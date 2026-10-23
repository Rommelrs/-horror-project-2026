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
    // coward freezes in front of him (C1).
    static readonly Vector3 K0 = new Vector3(-180.1f, Floor, -32.3f);    // the trigger, just out of the rope gate
    static readonly Vector3 K1 = new Vector3(-176.3f, Floor, -29.8f);
    const float KhalidWalkYaw = 56.7f;                                    // gate -> K1
    static readonly Vector3 C1 = new Vector3(-177.6f, Floor, -28.0f);    // 2.2 m from Khalid
    // Coward_Walk.anim = WalkForward baked into a straight walk at 1.32 m/s (5.6 s long). He reaches C1 at CowardArrive,
    // coming from just south of the hand truck (the line clears the barrel's west end by ~0.3 m).
    const float CowardWalkSpeed = 1.32f, CowardArrive = 5.5f;
    static readonly Vector3 WalkFrom = new Vector3(-178.6f, Floor, -21.0f);
    static readonly float WalkYaw = YawTo(WalkFrom, C1);
    static readonly Vector3 WalkStart = C1 + (WalkFrom - C1).normalized * CowardWalkSpeed * CowardArrive;
    // the baked walk heads 2.5 deg right of where its body faces (measured in Play): turned back so he ends on C1
    const float WalkHeadingFix = -2.5f;
    static readonly float K1Yaw = YawTo(K1, C1);              // facing the coward at C1
    static readonly float CowardYaw = YawTo(C1, K1);          // facing Khalid
    // the side-step that blocks his way north: Left_Strafe_Walk, 0.5 s of it = 0.94 m to the coward's left
    static readonly Vector3 C2 = C1 + Quaternion.Euler(0f, CowardYaw - 90f, 0f) * Vector3.forward * 0.94f;
    static readonly Vector3 BlockDir = (C2 - K1).normalized;
    static readonly float K1YawBlock = YawTo(K1, C2);
    static readonly float BlockYaw = YawTo(C2, K1);
    // Khalid takes a step north to go past him (KB) and the coward side-steps into his way
    static readonly Vector3 KB = K1 + BlockDir * 0.45f;
    // He steps up to Khalid and grabs him by both shoulders (CutsceneArmIK puts his hands there): 0.46 m apart, square
    // on. Khalid's shoulders are ~0.35 m higher than his and his arms only 0.52 m long: further away and his arms
    // locked out straight like a zombie's.
    static readonly Vector3 G = KB + BlockDir * 0.46f;
    // Walk_Backwards_Fear backs 1.68 m away over 3.87 s; he stops after 1.0 of them, short of the barrel
    const float BackOff = 1.0f;
    // when the glass breaks he lets go and recoils a step (backing straight off from G, his arms went through Khalid)
    static readonly Vector3 GRecoil = G + BlockDir * 0.18f;
    static readonly Vector3 BackedOff = GRecoil + BlockDir * BackOff;

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
    // block / grab line: Khalid at KB -> coward at G
    static Vector3 GF => BlockDir;
    static Vector3 GR => Quaternion.Euler(0f, 90f, 0f) * GF;
    static Vector3 At(Vector3 p, float height) => new Vector3(p.x, Floor + height, p.z);

    class Line { public string id, spk, en, ar; public float dur; }
    static Dictionary<string, Line> lines;
    static StringTableCollectionRef subtitles;
    struct StringTableCollectionRef { public UnityEditor.Localization.StringTableCollection col; }

    [MenuItem("Tools/Cutscenes/Build Coward Meeting 1")]
    public static void Build()
    {
        LoadLines();
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
        Mark(marks, "C2", C2, CowardYaw);
        Mark(marks, "KB", KB, K1YawBlock);
        Mark(marks, "G", G, BlockYaw);
        var mSkip = Mark(marks, "KhalidAfterSkip", KB, K1YawBlock);
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
        var sfxAlcove = Source(audioRoot, "SFX_Coward", (WalkStart + C1) * 0.5f + Vector3.up * 0.3f, true, 1f, sfxGroup);
        var sfx2D = Source(audioRoot, "SFX_2D", Vector3.zero, false, 0.9f, sfxGroup);
        var wind = Source(audioRoot, "Wind", Vector3.zero, false, 0.32f, ambGroup);
        var windGust = Source(audioRoot, "WindGust", Vector3.zero, false, 0.55f, ambGroup);
        var drone = Source(audioRoot, "Drone", Vector3.zero, false, 0.5f, ambGroup);
        var glass = Source(audioRoot, "GlassFar", new Vector3(-170f, 4f, 15f), true, 1f, sfxGroup);
        glass.maxDistance = 250f; glass.rolloffMode = AudioRolloffMode.Linear; glass.minDistance = 5f;
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
        // "Look, no knife": from beside Khalid's gun at chest height, the coward head to knees with his open hands
        var cHands = Vcam(cams, "CM_Hands_Insert", Ax(0.3f, 0.65f, 1.4f), At(C1, 1.2f), 46f, cowardChest, 0.6f, null, 0f, 0.35f, 0.55f);
        // the two of them in profile, Khalid left, the coward right, the west wall behind
        var cSide = Vcam(cams, "CM_Side_Two", Ax(1.11f, 3.6f, 1.55f), Ax(1.11f, 0f, 1.15f), 45f);
        // the coward's singles: from between them on Khalid's side, he looks off frame-left at Khalid, pallets behind.
        // Framed on his chest: on his head the camera swam round with every nervous glance
        var cCowardCU = Vcam(cams, "CM_Coward_CU", Ax(0.95f, 0.55f, 1.6f), c1Head, 35f, cowardChest, 0.6f, null, 0f, 0.6f, 0.68f);
        var cCowardCUPush = Vcam(cams, "CM_Coward_CU_Hold", Ax(0.95f, 0.55f, 1.6f), c1Head, 35f, cowardChest, 0.6f, null, 0f, 0.6f, 0.68f);
        Drift(cCowardCUPush, new Vector3(0, 0, 0.1f), -1.2f, 7f);
        // one of the "coward" papers on the ground by his feet, seen from almost straight above
        var paper = NearestPaper(C1, 2.5f);
        var cPaper = Vcam(cams, "CM_Paper_Insert", paper + new Vector3(-0.18f, 0.93f, -0.18f), paper, 40f);
        var cCowardMCU = Vcam(cams, "CM_Coward_MCU", Ax(0.8f, 0.6f, 1.55f), c1Head, 36f, cowardChest, 0.55f, null, 0f, 0.6f, 0.62f);
        // block / grab / ending: all from Khalid's right-hand (east) side of the grab line too
        Vector3 blockMid = (KB + C2) * 0.5f, grabMid = (KB + G) * 0.5f;
        // (close in and wide: further out, the barrel sits in the right-hand foreground)
        var cBlock = Vcam(cams, "CM_Block_Wide", At(blockMid + GR * 2.6f, 1.6f), At(blockMid, 1.1f), 48f);
        var cGrab = Vcam(cams, "CM_Grab_Close", At(grabMid + GR * 2.4f, 1.6f), At(grabMid, 1.38f), 42f, null, 0.5f, noiseStrong, 1.0f);
        // "For what?!": a tight profile two-shot, both faces and the held arm, punched in on the grab's side angle (from
        // behind Khalid his back and sleeve hid the coward, and the barrel filled the rest)
        var cCowardCUGrab = Vcam(cams, "CM_Grab_Tight", At(grabMid + GR * 1.85f, 1.65f), At(grabMid, 1.55f), 40f);
        var cSideEnd = Vcam(cams, "CM_Side_Two_End", At(grabMid + GR * 2.6f + GF * 0.5f, 1.55f), At(grabMid + GF * 0.5f, 1.15f), 48f);
        // he backs off toward the barrel (it is where he runs): from far enough round Khalid's right that he isn't behind Khalid
        var cOtsKEnd = Vcam(cams, "CM_OTS_Khalid_End", At(KB - GF * 1.8f + GR * 1.8f, 1.85f), At(BackedOff, 1.45f), 32f, cowardHead, 0.62f, noiseMild, 0.5f, 0.5f, 0.42f);
        // from the crossroads' south-east corner: Khalid in the foreground, the coward running off north past the car.
        // Placed already looking at where the run starts, so the composer doesn't whip round on the first frame.
        var cExit = Vcam(cams, "CM_Exit_Track", new Vector3(-172.9f, 3.1f, -31.5f), At(BackedOff, 1.0f), 45f, cowardHips, 0.5f, null, 0f, 0.9f);
        // behind Khalid, looking up the alley where the coward went; blends back into the gameplay camera
        var cReturn = Vcam(cams, "CM_Return", At(KB - GF * 2.3f + GR * 0.3f, 1.8f), At(KB + GF * 4f, 1.35f), 45f);

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
        };

        BuildOpen(dOpen, ctx, new[] { cAlleyWide, cSpot, cKMCU, cOtsK, cOtsC, cOtsKPush, cKCU, cHands, cSide, cCowardCU, cPaper, cCowardMCU });
        BuildA(dA, ctx, new[] { cOtsK, cCowardMCU, cBlock, cGrab, cCowardCUGrab, cKCU });
        BuildB(dB, ctx, new[] { cOtsK, cCowardCU, cCowardCUPush, cBlock, cGrab, cCowardCUGrab });
        BuildEnd(dEnd, ctx, new[] { cSideEnd, cOtsKEnd, cExit, cReturn });

        // ── controller ──
        // across the rope gate's opening (z -33.7..-30.9), a step out into the crossroads
        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true; trigger.size = new Vector3(1.0f, 3f, 2.8f); trigger.center = new Vector3(0, 1.5f, 0);
        var cs = root.AddComponent<CowardMeetingCutscene>();
        cs.open = dOpen; cs.versionA = dA; cs.versionB = dB; cs.end = dEnd;
        cs.knifeHandle = AssetDatabase.LoadAssetAtPath<Item>("Assets/Resources/Items/Key items/Knife Handle.asset");
        cs.coward = coward; cs.cowardMover = mover; cs.cowardHead = cowardHead;
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
                // walks in, sees someone coming out of the dust (head first), stops and turns to him
                khalid = new[] { Key(0f, K0, 90f, true), Key(KhalidWalkEnd, K1, KhalidWalkYaw), Key(4.1f, K1, KhalidWalkYaw), Key(5.1f, K1, K1Yaw) },
                headLook = new[] { new Vector2(0f, 0f), new Vector2(3.4f, 0.8f), new Vector2(5.3f, 0f) },
                ambience = new[] { new Vector2(0f, 1f), new Vector2(7.0f, 0.71f) },
            },
            new CowardMeetingCutscene.Phase
            {
                name = "A",
                // "I have to." - he turns north and steps off to go past; the coward side-steps into his way
                khalid = new[] { Key(0f, K1, K1Yaw), Key(AStepStart, K1, K1Yaw), Key(AStepStart + KhalidStepTime, KB, K1YawBlock) },
                cowardLook = new[] { new Vector2(0f, 0f), new Vector2(14.8f, 0.85f) },
                grab = new[] { new Vector2(0f, 0f), new Vector2(AHeld - 0.3f, 1f) },
                // shakes him on "Can't you hear me?!" and "For what?!", holds still on "Let go of me."
                tug = new[] { new Vector2(0f, 0f), new Vector2(15.4f, 0.05f), new Vector2(18.8f, 0.008f), new Vector2(21.2f, 0.06f), new Vector2(23.7f, 0f) },
                headLook = new[] { new Vector2(0f, 0f), new Vector2(15.0f, 0.85f) },   // and he looks down at him
                ambience = new[] { new Vector2(0f, 0.71f) },
            },
            new CowardMeetingCutscene.Phase
            {
                name = "B",
                khalid = new[] { Key(0f, K1, K1Yaw), Key(BStepStart, K1, K1Yaw), Key(BStepStart + KhalidStepTime, KB, K1YawBlock) },
                cowardLook = new[] { new Vector2(0f, 0f), new Vector2(30.3f, 0.85f) },
                grab = new[] { new Vector2(0f, 0f), new Vector2(BHeld - 0.3f, 1f) },
                tug = new[] { new Vector2(0f, 0f), new Vector2(30.1f, 0.05f), new Vector2(33.7f, 0.008f), new Vector2(36.1f, 0.06f), new Vector2(38.6f, 0f) },
                headLook = new[] { new Vector2(0f, 0f), new Vector2(30.5f, 0.85f) },
                ambience = new[] { new Vector2(0f, 0.71f) },
            },
            new CowardMeetingCutscene.Phase
            {
                name = "End",
                // turns to watch him run off north
                khalid = new[] { Key(0f, KB, K1YawBlock), Key(8.3f, KB, K1YawBlock), Key(9.4f, KB, 355f) },
                cowardLook = new[] { new Vector2(0f, 0.85f), new Vector2(2.0f, 0f) },
                grab = new[] { new Vector2(0f, 1f), new Vector2(2.0f, 0f) },
                headLook = new[] { new Vector2(0f, 0.85f), new Vector2(2.0f, 0f) },
                ambience = new[] { new Vector2(0f, 0.5f), new Vector2(10.5f, 1f) },
            },
        };
        // the hidden gameplay camera is put behind him under the last shot, before the blend back
        cs.gameplayCameraSnapTime = 10.6f;

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
        public AudioSource voK, voC, sfxAlcove, sfx2D, wind, windGust, drone, glass;
    }

    // Khalid walks from the gate to K1 (~4.5 m) by this time
    const float KhalidWalkEnd = 3.9f;
    // his one step toward the alley (K1 -> KB) in A / B
    const float AStepStart = 10.3f, BStepStart = 24.2f, KhalidStepTime = 0.9f;

    // he aims at the coward from GunUp until "Look, no knife", then lowers the gun to his side for the rest
    const float GunUp = 6.3f, GunDown = 26.0f;

    // Khalid's aim for the scene, standing (his gameplay aim is a deep combat crouch that read as a frozen lunge): the
    // idle body with the aim's arms moved whole onto its shoulders (his rig is flat - forearms/hands are positioned in
    // root space - so copying the aim's arm curves onto the upright body had stretched the upper arms to 0.45 m)
    const string AimStand = Dir + "/Khalid_AimStand.anim";

    static void BuildOpen(PlayableDirector d, Ctx c, CinemachineVirtualCameraBase[] v)
    {
        var tl = NewTimeline(d, "TL_Coward_Open");
        const float len = 57f;
        Activation(tl, d, c.coward, 0, len, ActivationTrack.PostPlaybackState.LeaveAsIs);

        var k = Anim(tl, d, "Khalid", c.khalid, TrackOffset.ApplySceneOffsets);
        KClip(k, "rig|khalid_walk", 0f, KhalidWalkEnd + 0.25f, true);   // replaced below by Walk_SH if present
        // the gameplay aim_raise clip crouches into the combat stance: the gun comes up by cross-fading instead
        KClip(k, "rig|khalid_idle", KhalidWalkEnd - 0.15f, GunUp + 0.4f, true);
        KClip(k, AimStand, GunUp, GunDown + 0.6f, true);
        // "Look, no knife" -> he lowers the gun to his side
        KClip(k, "rig|khalid_idle", GunDown, len, true, 0.6f);

        // walks down the alley not looking; startles when he sees Khalid, then cowers as the gun comes up
        var cw = Anim(tl, d, "Coward", c.cowardAnim, TrackOffset.ApplyTransformOffsets);
        var walk = cw.CreateClip(AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir + "/Coward_Walk.anim"));
        walk.start = 0f; walk.duration = CowardArrive + 0.1f;
        Offsets(walk, WalkStart, WalkYaw + WalkHeadingFix, false);
        Chain(cw, "Reacting_Surprised", CowardArrive - 0.25f, GunUp + 0.6f, C1, CowardYaw, 0.2f);
        Chain(cw, "Terrified", GunUp + 0.1f, 14.8f, C1, CowardYaw, TerrifiedIn);
        Chain(cw, "Talking_TwoHands", 14.3f, 22.6f, C1, CowardYaw);
        Chain(cw, "Hands_Forward_Gesture", 22.1f, 26.4f, C1, CowardYaw);
        // calmer once the gun is down: upright and jumpy (Terrified's deep crouch hid his face in every close-up)
        Chain(cw, "Nervously_Look_Around", 25.9f, 42.7f, C1, CowardYaw);
        Chain(cw, "Look_Over_Shoulder", 42.2f, 42.2f + Mixamo("Look_Over_Shoulder").length, C1, CowardYaw);
        Chain(cw, "Nervously_Look_Around", 45.5f, 51.0f, C1, CowardYaw, 2.0f);
        Chain(cw, "Talking_TwoHands", 50.5f, 54.6f, C1, CowardYaw);       // "...Is he yours?"
        Chain(cw, "Nervously_Look_Around", 54.1f, len, C1, CowardYaw, 4.0f);

        var cm = Cameras(tl, d, c.brain);
        Shot(cm, d, v[0], 0f, 4f);   // (opens under the fade from black)
        Shot(cm, d, v[1], 4f, GunUp);   // he spots him
        Shot(cm, d, v[2], GunUp, 8f);   // the gun comes up
        Shot(cm, d, v[3], 8f, 12.3f);
        Shot(cm, d, v[4], 12.3f, 14.3f);
        Shot(cm, d, v[5], 14.3f, 19.5f);
        Shot(cm, d, v[6], 19.5f, 22.2f);
        Shot(cm, d, v[7], 22.2f, 26f);
        Shot(cm, d, v[8], 26f, 32.5f);
        Shot(cm, d, v[9], 32.5f, 35f);
        Shot(cm, d, v[3], 35f, 37.5f);
        Shot(cm, d, v[4], 37.5f, 39.4f);
        Shot(cm, d, v[3], 39.4f, 41f);
        Shot(cm, d, v[10], 41f, 42.5f);
        Shot(cm, d, v[11], 42.5f, 46.4f);
        Shot(cm, d, v[8], 46.4f, 50.9f);    // "I'm looking for a boy."
        Shot(cm, d, v[9], 50.9f, 54.4f);    // "...Is he yours?"
        Shot(cm, d, v[6], 54.4f, len);      // "...He's my brother."

        var (voK, voC) = VoiceTracks(tl, d, c);
        Say(tl, voK, voC, "CW1_001", 8.2f); Say(tl, voK, voC, "CW1_002", 12.5f); Say(tl, voK, voC, "CW1_003", 14.4f);
        Say(tl, voK, voC, "CW1_004", 19.6f); Say(tl, voK, voC, "CW1_005", 22.3f); Say(tl, voK, voC, "CW1_006", 27.6f);
        Say(tl, voK, voC, "CW1_007", 29.5f); Say(tl, voK, voC, "CW1_008", 31.0f); Say(tl, voK, voC, "CW1_009", 35.1f);
        Say(tl, voK, voC, "CW1_010", 37.6f); Say(tl, voK, voC, "CW1_011", 39.5f); Say(tl, voK, voC, "CW1_012", 43.4f);
        Say(tl, voK, voC, "CW1_013", 46.6f); Say(tl, voK, voC, "CW1_014", 51.05f); Say(tl, voK, voC, "CW1_015", 54.5f);

        // his footsteps in the sand (a step every 0.63 s of the walk)
        var sfx = AudioTrackFor(tl, d, "SFX (coward)", c.sfxAlcove);
        for (int i = 0; i < 9; i++) Sound(sfx, "Assets/Audio/Footsteps/Sand/Sand" + (i % 4 + 1) + ".mp3", 0.3f + i * 0.63f);
        var sfx2 = AudioTrackFor(tl, d, "SFX", c.sfx2D);
        Sound(sfx2, "Assets/Audio/NewSFX/pulls_out_gun.wav", GunUp);
        var amb = AudioTrackFor(tl, d, "Ambience (wind)", c.wind);
        Loop(amb, AudioDir + "/Wind_Loop.wav", 0f, len);
        var dr = AudioTrackFor(tl, d, "Drone", c.drone);
        var drClip = Loop(dr, AudioDir + "/Drone_Loop.wav", 32.5f, len); drClip.easeInDuration = 3f;
        Finish(tl);
    }

    static void BuildA(PlayableDirector d, Ctx c, CinemachineVirtualCameraBase[] v)
    {
        var tl = NewTimeline(d, "TL_Coward_A");
        const float len = 25.5f;
        Activation(tl, d, c.coward, 0, len, ActivationTrack.PostPlaybackState.LeaveAsIs);
        // (the retargeted "pull free" clip read as Khalid being shoved round; he holds still while held)
        var k = Anim(tl, d, "Khalid", c.khalid, TrackOffset.ApplySceneOffsets);
        KhalidBlock(k, AStepStart, len);

        var cw = Anim(tl, d, "Coward", c.cowardAnim, TrackOffset.ApplyTransformOffsets);
        Chain(cw, "Talking_TwoHands", 0f, 4.3f, C1, CowardYaw);
        // "People go in. They don't come out." - looking round at the alleys
        Chain(cw, "Nervously_Look_Around", 3.8f, 10.9f, C1, CowardYaw);
        // steps into his way and stands there, arms out (a crouched pose doesn't block anyone)
        CClip(cw, "Left_Strafe_Walk", 10.6f, 11.1f, C1, CowardYaw, false);
        Chain(cw, "Hands_Forward_Gesture", 10.85f, 14.3f, C2, BlockYaw);
        // steps up to him, grabs him by the shoulders (CutsceneArmIK, weight keys in the phase) and pleads holding on
        // (Intercepting flung one arm out and lunged into him; Yelling_Lean is a crouched yell that read as sitting on air)
        float held = StepIn(cw, 14.0f);
        APleadPhase = Chain(cw, "Talking_TwoHands", held - 0.3f, len, G, BlockYaw);
        AHeld = held;

        var cm = Cameras(tl, d, c.brain);
        Shot(cm, d, v[0], 0f, 4.0f);
        Shot(cm, d, v[1], 4.0f, 8.7f);
        Shot(cm, d, v[5], 8.7f, 10.6f);     // "I have to."
        Shot(cm, d, v[2], 10.6f, 14.0f);
        Shot(cm, d, v[3], 14.0f, 21.0f);
        Shot(cm, d, v[4], 21.0f, len);

        var (voK, voC) = VoiceTracks(tl, d, c);
        Say(tl, voK, voC, "CW1_A01", 0.3f); Say(tl, voK, voC, "CW1_A02", 4.3f); Say(tl, voK, voC, "CW1_A03", 8.8f);
        Say(tl, voK, voC, "CW1_A04", 11.3f); Say(tl, voK, voC, "CW1_A05", 12.9f); Say(tl, voK, voC, "CW1_A06", 15.3f);
        Say(tl, voK, voC, "CW1_A07", 19.1f); Say(tl, voK, voC, "CW1_A08", 21.2f);

        var sfx = AudioTrackFor(tl, d, "SFX (coward)", c.sfxAlcove);
        Sound(sfx, "Assets/Audio/Footsteps/Sand/Sand2.mp3", 10.7f); Sound(sfx, "Assets/Audio/Footsteps/Sand/Sand3.mp3", 10.95f);
        Sound(sfx, "Assets/Audio/Cloth/Cloth rip edited.mp3", 15.1f, 0.5f);
        Loop(AudioTrackFor(tl, d, "Ambience (wind)", c.wind), AudioDir + "/Wind_Loop.wav", 0f, len);
        Loop(AudioTrackFor(tl, d, "Drone", c.drone), AudioDir + "/Drone_Loop.wav", 0f, len);
        Finish(tl);
    }

    static void BuildB(PlayableDirector d, Ctx c, CinemachineVirtualCameraBase[] v)
    {
        var tl = NewTimeline(d, "TL_Coward_B");
        const float len = 39.3f;
        Activation(tl, d, c.coward, 0, len, ActivationTrack.PostPlaybackState.LeaveAsIs);
        var k = Anim(tl, d, "Khalid", c.khalid, TrackOffset.ApplySceneOffsets);
        KhalidBlock(k, BStepStart, len);

        var cw = Anim(tl, d, "Coward", c.cowardAnim, TrackOffset.ApplyTransformOffsets);
        Chain(cw, "Talking_TwoHands", 0f, 4.0f, C1, CowardYaw);
        Chain(cw, "Terrified", 3.5f, 24.8f, C1, CowardYaw, TerrifiedIn);
        CClip(cw, "Left_Strafe_Walk", 24.5f, 25.0f, C1, CowardYaw, false);
        Chain(cw, "Hands_Forward_Gesture", 24.75f, 29.8f, C2, BlockYaw);
        float held = StepIn(cw, 29.5f);
        Chain(cw, "Talking_TwoHands", held - 0.3f, len, G, BlockYaw);
        BHeld = held;

        var cm = Cameras(tl, d, c.brain);
        Shot(cm, d, v[0], 0f, 8.0f);
        Shot(cm, d, v[1], 8.0f, 17.2f);
        Shot(cm, d, v[2], 17.2f, 24.5f);
        Shot(cm, d, v[3], 24.5f, 29.5f);
        Shot(cm, d, v[4], 29.5f, 36.0f);
        Shot(cm, d, v[5], 36.0f, len);

        var (voK, voC) = VoiceTracks(tl, d, c);
        Say(tl, voK, voC, "CW1_B01", 0.3f); Say(tl, voK, voC, "CW1_B02", 4.0f); Say(tl, voK, voC, "CW1_B03", 8.3f);
        Say(tl, voK, voC, "CW1_B04", 12.0f); Say(tl, voK, voC, "CW1_B05", 13.3f); Say(tl, voK, voC, "CW1_B06", 15.7f);
        Say(tl, voK, voC, "CW1_B07", 18.7f); Say(tl, voK, voC, "CW1_B08", 21.1f); Say(tl, voK, voC, "CW1_B09", 25.2f);
        Say(tl, voK, voC, "CW1_B10", 28.4f); Say(tl, voK, voC, "CW1_B11", 30.0f); Say(tl, voK, voC, "CW1_B12", 33.9f);
        Say(tl, voK, voC, "CW1_B13", 36.1f);

        var sfx = AudioTrackFor(tl, d, "SFX (coward)", c.sfxAlcove);
        Sound(sfx, "Assets/Audio/Footsteps/Sand/Sand2.mp3", 24.6f); Sound(sfx, "Assets/Audio/Footsteps/Sand/Sand3.mp3", 24.85f);
        Sound(sfx, "Assets/Audio/Cloth/Cloth rip edited.mp3", 30.6f, 0.5f);
        Loop(AudioTrackFor(tl, d, "Ambience (wind)", c.wind), AudioDir + "/Wind_Loop.wav", 0f, len);
        Loop(AudioTrackFor(tl, d, "Drone", c.drone), AudioDir + "/Drone_Loop.wav", 0f, len);
        Finish(tl);
    }

    static void BuildEnd(PlayableDirector d, Ctx c, CinemachineVirtualCameraBase[] v)
    {
        var tl = NewTimeline(d, "TL_Coward_End");
        const float len = 13.5f;
        Activation(tl, d, c.coward, 0, 12.5f, ActivationTrack.PostPlaybackState.Inactive);

        var k = Anim(tl, d, "Khalid", c.khalid, TrackOffset.ApplySceneOffsets);
        KClip(k, "rig|khalid_idle", 0f, len, true);

        var cw = Anim(tl, d, "Coward", c.cowardAnim, TrackOffset.ApplyTransformOffsets);
        // still pleading where A left off (same point of the clip, so nothing pops), lets go when the glass breaks at 2 s
        Chain(cw, "Talking_TwoHands", 0f, 2.35f, G, BlockYaw, APleadPhase);
        Chain(cw, "Reacting_Surprised", 2.0f, 3.45f, GRecoil, BlockYaw, 0.2f);
        float backEnd = 3.15f + 3.87f * BackOff / 1.68f;
        CClip(cw, "Walk_Backwards_Fear", 3.15f, backEnd, GRecoil, BlockYaw, false);
        // he stops with his feet together (2.3 s into the clip) and sinks into the cower from there
        Chain(cw, "Terrified", backEnd - 0.5f, RunStart + 0.3f, BackedOff, BlockYaw, TerrifiedIn + 0.2f);
        RunLegs(cw);

        var cm = Cameras(tl, d, c.brain);
        Shot(cm, d, v[0], 0f, 4f);
        Shot(cm, d, v[1], 4f, 8f);
        Shot(cm, d, v[2], 8f, 10f);   // he's lost in the dust by ~9.5 s
        var back = Shot(cm, d, v[3], 10f, len); back.easeOutDuration = 1.5f;   // a beat on Khalid, then back to play

        var (voK, voC) = VoiceTracks(tl, d, c);
        Say(tl, voK, voC, "CW1_E01", 4.4f); Say(tl, voK, voC, "CW1_E02", 8.6f);

        Sound(AudioTrackFor(tl, d, "Glass (far)", c.glass), "Assets/Audio/Glass Debris/GlassDestroy.wav", 2.0f);
        var sfx = AudioTrackFor(tl, d, "SFX (coward)", c.sfxAlcove);
        Sound(sfx, "Assets/Audio/Footsteps/Sand/Sand1.mp3", 3.6f); Sound(sfx, "Assets/Audio/Footsteps/Sand/Sand4.mp3", 4.5f);
        var sfx2 = AudioTrackFor(tl, d, "SFX", c.sfx2D);
        Sound(sfx2, "Assets/Audio/Footsteps/Sand/Sand2.mp3", 8.25f); Sound(sfx2, "Assets/Audio/Footsteps/Sand/Sand3.mp3", 8.5f);
        Sound(sfx2, "Assets/Audio/Footsteps/Sand/Sand1.mp3", 8.75f); Sound(sfx2, "Assets/Audio/Footsteps/Sand/Sand4.mp3", 9.0f);
        Loop(AudioTrackFor(tl, d, "Ambience (wind)", c.wind), AudioDir + "/Wind_Loop.wav", 0f, len);
        var gust = Loop(AudioTrackFor(tl, d, "Wind returns", c.windGust), AudioDir + "/Wind_Loop.wav", 10.5f, len); gust.easeInDuration = 1f; gust.easeOutDuration = 1.2f;
        Loop(AudioTrackFor(tl, d, "Drone", c.drone), AudioDir + "/Drone_Loop.wav", 0f, 2.0f);
        Finish(tl);
    }

    static readonly Vector3 ExitEnd = new Vector3(-177.5f, Floor, 9.0f);
    // west of the blue barrel (x -177.5..-175.8, z -26.7..-25.5): he runs there first, then straight up the alley between
    // the hand truck and the white car into the dust
    static readonly Vector3 ExitTurn = new Vector3(-177.95f, Floor, -26.1f);
    const float RunStart = 8.0f, RunSpeed = 4.7f;

    // set while building A / B, used by the phases and by End
    static float AHeld, BHeld, APleadPhase;

    // Coward_Run.anim (the in-place Run baked to 4.7 m/s root motion): one leg to the turn, one up the alley. The second
    // leg carries the run cycle on (clipIn) and is placed back by the distance that skips, since the clip's start
    // offset is removed at its first frame.
    static void RunLegs(AnimationTrack cw)
    {
        var runClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir + "/Coward_Run.anim");
        float leg1 = Vector3.Distance(BackedOff, ExitTurn) / RunSpeed;
        var a = cw.CreateClip(runClip);
        a.start = RunStart; a.duration = leg1 + 0.2f;
        Offsets(a, BackedOff, YawTo(BackedOff, ExitTurn), false);
        var b = cw.CreateClip(runClip);
        float yaw2 = YawTo(ExitTurn, ExitEnd);
        b.start = RunStart + leg1 - 0.1f; b.clipIn = leg1 - 0.1f;
        b.duration = 12.5f - (float)b.start;
        Offsets(b, ExitTurn - Quaternion.Euler(0f, yaw2, 0f) * Vector3.forward * RunSpeed * (leg1 - 0.1f), yaw2, false);
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

    // the coward's two quick steps from where he blocks (C2) up to Khalid (G); returns when he gets there
    static float StepIn(AnimationTrack cw, float start)
    {
        const float rate = 1.3f;
        float dur = Vector3.Distance(C2, G) / (CowardWalkSpeed * rate);
        var c = cw.CreateClip(AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir + "/Coward_Walk.anim"));
        c.start = start; c.duration = dur + 0.15f; c.timeScale = rate;
        Offsets(c, C2, YawTo(C2, G), false);
        return start + dur;
    }

    // Khalid in A / B: gun down at his side; one step toward the alley (the controller moves him K1 -> KB)
    static void KhalidBlock(AnimationTrack k, float step, float len)
    {
        KClip(k, "rig|khalid_idle", 0f, step + 0.3f, true);
        KClip(k, "rig|khalid_walk", step - 0.05f, step + KhalidStepTime + 0.15f, true);
        KClip(k, "rig|khalid_idle", step + KhalidStepTime - 0.25f, len, true);
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
        d.playableAsset = tl;
        return tl;
    }

    static void Finish(TimelineAsset tl) { FixBlends(tl); EditorUtility.SetDirty(tl); }

    static void Activation(TimelineAsset tl, PlayableDirector d, GameObject go, float start, float end, ActivationTrack.PostPlaybackState post)
    {
        var t = tl.CreateTrack<ActivationTrack>(null, go.name + " active");
        var clip = t.CreateDefaultClip(); clip.start = start; clip.duration = end - start;
        t.postPlaybackState = post;
        d.SetGenericBinding(t, go);
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

    static TimelineClip Loop(AudioTrack t, string path, float start, float end)
    {
        var clip = t.CreateClip(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
        clip.start = start; clip.duration = end - start;
        ((AudioPlayableAsset)clip.asset).loop = true;
        return clip;
    }

    // one voice placeholder clip on the speaker's track + the subtitle marker on the timeline's marker row
    static void Say(TimelineAsset tl, AudioTrack k, AudioTrack c, string id, float start)
    {
        var line = lines[id];
        var track = line.spk == "K" ? k : c;
        string wav = AudioDir + "/" + id + "_" + (line.spk == "K" ? "Khalid" : "Coward") + ".wav";
        var clip = track.CreateClip(AssetDatabase.LoadAssetAtPath<AudioClip>(wav));
        clip.start = start; clip.displayName = id;
        if (tl.markerTrack == null) tl.CreateMarkerTrack();
        var marker = tl.markerTrack.CreateMarker<SubtitleMarker>(start + 0.1);
        var entry = subtitles.col.SharedData.GetEntry(line.en);
        marker.line = new LocalizedString(subtitles.col.TableCollectionName, entry.Id);
        marker.duration = line.dur - 0.15f;
        marker.lineId = id;
        marker.name = id;
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

    // the ground paper ("coward") closest to a point, for the paper insert
    static Vector3 NearestPaper(Vector3 near, float maxDistance)
    {
        Vector3 best = near; float bestD = maxDistance;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.name.StartsWith("Paper") || r.bounds.size.y > 0.2f) continue;
            float dist = Vector2.Distance(new Vector2(r.bounds.center.x, r.bounds.center.z), new Vector2(near.x, near.z));
            if (dist < bestD) { bestD = dist; best = r.bounds.center; }
        }
        return best;
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

    static void LoadLines()
    {
        lines = new Dictionary<string, Line>();
        foreach (var raw in File.ReadAllLines("AnimationTools/cutscene_coward/lines.tsv", System.Text.Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var f = raw.Split('\t');
            lines[f[0]] = new Line { id = f[0], spk = f[1], en = f[2], ar = f[3], dur = float.Parse(f[4], System.Globalization.CultureInfo.InvariantCulture) };
        }
        subtitles = new StringTableCollectionRef { col = LocalizationEditorSettings.GetStringTableCollection("Subtitles") };
    }
}
