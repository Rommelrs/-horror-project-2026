using System;
using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// "The coward's first meeting" (Alley 1). Plays once, the first time the player walks back past the alcove with the
// knife handle: Open -> A (Alley 2 not visited) or B (Alley 2 visited) -> End, each its own PlayableDirector/Timeline.
// Khalid is the real player (his Animator is driven by the timelines while the player components are switched off);
// this script moves his root between the scene marks, turns his head, ducks the ambience and handles skipping.
// The coward is a separate NPC that never takes damage (no enemy components), disabled outside the cutscene.
[DefaultExecutionOrder(-100)]
public class CowardMeetingCutscene : MonoBehaviour
{
    public const string DoneFlag = "CowardMeeting1Done";
    public const string Alley2Flag = "Alley2Visited";

    [Serializable]
    public struct PoseKey
    {
        public float time;
        public Vector3 position;
        public float yaw;
        [Tooltip("Key 0 of the opening: start from wherever the player actually is when the trigger fires.")]
        public bool fromCurrent;
        [Tooltip("Reach this key at constant speed instead of easing in and out (a walk passing through it).")]
        public bool linear;
        [Tooltip("A camera cut: hold the previous key until this one's time, then jump here (no in-between frames).")]
        public bool cut;
    }

    [Serializable]
    public class Phase
    {
        public string name;
        [Tooltip("Khalid's root (the Player object) over this timeline's time.")]
        public PoseKey[] khalid = new PoseKey[0];
        [Tooltip("The coward's mover over this timeline's time (only used for his exit run; root motion moves him otherwise).")]
        public PoseKey[] cowardMover = new PoseKey[0];
        [Tooltip("x = time, y = head-look weight (Khalid turning his head toward the coward).")]
        public Vector2[] headLook = new Vector2[0];
        [Tooltip("x = time, y = what Khalid looks at from then on: -1 the coward's head, else an index into lookPoints. His gaze eases across.")]
        public Vector2[] headLookAt = new Vector2[0];
        [Tooltip("x = time, y = head-look weight of the coward toward Khalid's face (while he pleads holding him).")]
        public Vector2[] cowardLook = new Vector2[0];
        [Tooltip("x = time, y = weight of the coward's hands on Khalid's shoulders.")]
        public Vector2[] grab = new Vector2[0];
        [Tooltip("x = time, y = how hard (metres) he tugs at Khalid's shoulders while holding them.")]
        public Vector2[] tug = new Vector2[0];
        [Tooltip("x = time, y = ambience volume multiplier (1 = normal, 0.71 = -3 dB, 0.5 = -6 dB).")]
        public Vector2[] ambience = new Vector2[0];
        [Tooltip("x = time, y = 0..1 of gunDipAngle: Khalid lowering his aimed gun a little (straight lines between keys).")]
        public Vector2[] gunDip = new Vector2[0];
    }

    [Header("Timelines")]
    public PlayableDirector open;
    public PlayableDirector versionA;
    public PlayableDirector versionB;
    public PlayableDirector end;
    [Tooltip("0 Open, 1 A, 2 B, 3 End")]
    public Phase[] phases = new Phase[4];
    [Tooltip("Off: always plays version A. On: plays version B instead when Alley 2 has already been visited.")]
    public bool versionBEnabled;
    [Tooltip("Muted source the other version's tracks are bound to at the start (tracks named \"... [A]\" / \"... [B]\" in "
           + "timelines both versions play): silences them and drops their subtitles.")]
    public AudioSource otherVersionSink;

    [Header("Conditions")]
    public Item knifeHandle;

    [Header("Actors")]
    public GameObject coward;
    public Transform cowardMover;
    public Transform cowardHead;
    [Tooltip("Khalid's voice source: kept at his head while the cutscene runs.")]
    public Transform khalidVoice;
    [Tooltip("Other things Khalid looks at (phase headLookAt): off toward the alley, the window the glass breaks in.")]
    public Transform[] lookPoints = new Transform[0];
    [Tooltip("Enemies under this root that are still alive when the scene starts are despawned (Alley 1).")]
    public Transform alleyEnemiesRoot;
    public float despawnRadius = 60f;

    [Tooltip("The cutscene's virtual cameras: only switched on while the scene plays, so they never compete with the gameplay camera.")]
    public GameObject camerasRoot;

    [Header("Where Khalid is left after a skip")]
    public Transform skipKhalidMark;
    [Tooltip("Cycle offset of the walk clip in Khalid's Animator Controller (WalkMove), set by the builder: the end hands "
           + "his stride over to it at the same point.")]
    public float walkCycleOffset;

    [Header("Left as the scene leaves it (the chair he knocks over)")]
    public Transform tippedChairPivot;
    public Vector3 tippedChairLocalPosition;
    public Vector3 tippedChairLocalEuler;

    [Tooltip("End timeline time at which the (hidden) gameplay camera is snapped behind Khalid, before the blend back.")]
    public float gameplayCameraSnapTime = 11.5f;

    [Header("Skip")]
    [Tooltip("Hold time if the scene has no CutsceneSkipper. Normally the skipper's hold time and its \"Hold E to skip\" "
           + "prompt are used, so every cutscene's prompt looks the same.")]
    public float skipHoldTime = 1f;
    public CutsceneSubtitleReceiver[] subtitleReceivers;

    [Tooltip("Where the coward's wrists go on Khalid's shoulders: from each shoulder joint, in Khalid's space (x right, y up, z forward).")]
    public Vector3 grabOffset = new Vector3(0f, 0.06f, 0.13f);
    [Tooltip("Push-pulls a second while the coward shakes Khalid by the shoulders.")]
    public float shakeRate = 1.7f;
    [Tooltip("Multiplies the phases' tug (how far Khalid's shoulders go, metres).")]
    public float shakeGain = 0.45f;
    [Tooltip("Degrees the coward's hands tremble (he's scared the whole scene).")]
    public float handTremble = 4f;
    [Tooltip("How far the coward's walk-in is bent into the scared posture (hunched, shoulders up, arms clutched to his chest); the rest is the walk's own arm swing.")]
    [Range(0f, 1f)] public float scaredWalk = 0.85f;
    [Tooltip("Open timeline time the scared posture has faded out by (he arrives and startles); the builder sets it.")]
    public float scaredWalkEnd = 5.5f;
    [Tooltip("How far the coward puts his hands up (surrendering) while Khalid aims at him.")]
    [Range(0f, 1f)] public float surrender = 0.9f;
    [Tooltip("Open timeline times: his hands are fully up by surrenderUp and start coming down at surrenderDown; the builder sets them.")]
    public float surrenderUp = 7.1f, surrenderDown = 26.2f;
    [Tooltip("Where his wrists go with his hands up: from each shoulder joint, in his space (x outward, y up, z forward).")]
    public Vector3 handsUpOffset = new Vector3(0.16f, 0.16f, 0.32f);

    [Tooltip("Degrees Khalid's arms and gun tip down about his shoulders at gunDip 1 (\"...Other versions?\").")]
    public float gunDipAngle = 12f;

    [Tooltip("The cutscene starts by fading to black and back in on its first shot (seconds each way).")]
    public float startFade = 0.5f;

    [Header("Debug")]
    [Tooltip("Ignore the knife-handle / already-played checks (for testing in the editor).")]
    public bool ignoreConditions;
    [Tooltip("Debug play-now: switched off / on first (the unburned and the burned rope gate).")]
    public GameObject[] debugHide = new GameObject[0];
    public GameObject[] debugShow = new GameObject[0];

    public static bool IsPlaying { get; private set; }

    readonly List<Behaviour> disabledPlayerBehaviours = new List<Behaviour>();
    PlayableDirector current;
    int currentPhase = -1;
    bool running, skipping, gameplayCameraSnapped;
    float skipHeld;
    Vector3 startPos; float startYaw;
    CutsceneHeadLook headLook, cowardHeadLook;
    Animator khalidAnim;
    Transform khalidGaze; Vector3 gazeVel;
    CutsceneArmIK cowardGrab, cowardHandsUp;
    float tug;
    // being shaken (see ShakeKhalid)
    Transform[] kSpine, kCarry, kArmBones = new Transform[0];
    Transform kChest, kHead, kArmTwistL, kArmTwistR;
    Transform[] cSpine, cTremble; Transform cHead, cHips;
    HumanPoseHandler cPose; HumanPose cHumanPose;
    Spring torsoPitch, torsoRoll, headPitch, headRoll, armSwing;
    float shakeClock;
    Collider trigger;

    void Awake()
    {
        trigger = GetComponent<Collider>();
        foreach (var d in Directors()) if (d != null) { d.playOnAwake = false; d.extrapolationMode = DirectorWrapMode.Hold; }
    }

    void Start()
    {
        if (coward != null) coward.SetActive(false);
        if (camerasRoot != null) camerasRoot.SetActive(false);
        if (StoryFlags.Has(DoneFlag) && !ignoreConditions)
        {
            if (trigger != null) trigger.enabled = false;
            ApplyAftermath();
        }
    }

    void ApplyAftermath()
    {
        if (tippedChairPivot != null)
            tippedChairPivot.SetLocalPositionAndRotation(tippedChairLocalPosition, Quaternion.Euler(tippedChairLocalEuler));
    }

    IEnumerable<PlayableDirector> Directors() { yield return open; yield return versionA; yield return versionB; yield return end; }

    void OnTriggerEnter(Collider other)
    {
        if (running || !other.CompareTag("Player")) return;
        if (!ignoreConditions && (StoryFlags.Has(DoneFlag) || !PlayerHasKnifeHandle())) return;
        Begin();
    }

    bool PlayerHasKnifeHandle()
    {
        if (Player.instance == null || Player.instance.inventory == null) return false;
        foreach (var stack in Player.instance.inventory.GetItems())
            if (stack.item != null && (stack.item == knifeHandle || (knifeHandle == null && stack.item.name == "Knife Handle"))) return true;
        return false;
    }

    // ───────────────────────── start / end ─────────────────────────

    public void Begin()
    {
        var player = Player.instance;
        if (player == null || running) return;
        running = true; IsPlaying = true;
        if (trigger != null) trigger.enabled = false;

        // gun away (the cutscene raises it itself), no input, no damage
        if (player.playerWeaponSystem != null) player.playerWeaponSystem.ExitOutOfAiming();
        DisableBehaviour(player.GetComponent<PlayerMovement>());
        DisableBehaviour(player.playerWeaponSystem);
        DisableBehaviour(player.GetComponent<PlayerRoll>());
        DisableBehaviour(player.GetComponent<PlayerMeleeAttack>());
        if (player.controller != null) player.controller.enabled = false;
        if (player.health != null) player.health.Invincible = true;
        if (CameraSystem.Instance != null) CameraSystem.Instance.ForceShowPlayer = true;

        gameplayCameraSnapped = false;
        startPos = player.transform.position;
        startYaw = player.transform.eulerAngles.y;

        DespawnAlleyEnemies();
        UseVersion(PlaysB ? "B" : "A");
        BindKhalid(player);
        SetupHeadLook(player);

        StartCoroutine(Co_Run());
    }

    /// <summary>Debug: play the cutscene now from the gate, without the knife handle (Play mode only).</summary>
    [ContextMenu("Debug: Play Now")]
    public void DebugPlayNow()
    {
        var player = Player.instance;
        if (!Application.isPlaying || running || player == null) return;
        foreach (var g in debugHide)
        {
            if (g == null) continue;
            // the rope gate is shown burned: its "These ropes are blocking the way" goes with it
            var rope = g.GetComponentInParent<BurnableObstacle>(true);
            if (rope != null) { rope.enabled = false; foreach (var c in rope.GetComponents<Collider>()) c.enabled = false; }
            g.SetActive(false);
        }
        foreach (var g in debugShow) if (g != null) g.SetActive(true);
        // stand him in the trigger, facing out of the gate
        if (player.controller != null) player.controller.enabled = false;
        player.transform.SetPositionAndRotation(new Vector3(transform.position.x, player.transform.position.y, transform.position.z), Quaternion.Euler(0f, 90f, 0f));
        if (player.controller != null) player.controller.enabled = true;
        player.OnPlayerTelported?.Invoke();
        Begin();
    }

    void DisableBehaviour(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false;
        disabledPlayerBehaviours.Add(b);
    }

    void DespawnAlleyEnemies()
    {
        if (alleyEnemiesRoot == null) return;
        foreach (var e in alleyEnemiesRoot.GetComponentsInChildren<Enemy>())
        {
            if (e.health != null && e.health.IsDead) continue;
            if (Vector3.Distance(e.transform.position, transform.position) > despawnRadius) continue;
            e.gameObject.SetActive(false);
        }
    }

    bool PlaysB => versionBEnabled && StoryFlags.Has(Alley2Flag);

    // Tracks named "... [A]" / "... [B]" belong to one version (in timelines both versions play: the Open's second line,
    // the End's music). The other version's go to the muted sink - no sound, and no receiver there for their subtitles.
    void UseVersion(string version)
    {
        if (otherVersionSink == null) return;
        foreach (var d in Directors())
        {
            if (d == null || !(d.playableAsset is TimelineAsset tl)) continue;
            bool changed = false;
            foreach (var track in tl.GetOutputTracks())
            {
                string n = track.name;
                if (!(n.EndsWith(" [A]") || n.EndsWith(" [B]")) || n.EndsWith(" [" + version + "]")) continue;
                d.SetGenericBinding(track, otherVersionSink);
                changed = true;
            }
            if (changed && d.playableGraph.IsValid()) d.RebuildGraph();
        }
    }

    // the timelines' "Khalid" animation tracks follow whatever player object exists at runtime
    void BindKhalid(Player player)
    {
        Animator anim = null;
        foreach (var a in player.GetComponentsInChildren<Animator>())
            if (a.name == "MainCharacterAnimated") { anim = a; break; }
        if (anim == null) return;
        khalidAnim = anim;
        foreach (var d in Directors())
        {
            if (d == null || d.playableAsset == null) continue;
            foreach (var output in d.playableAsset.outputs)
                if (output.streamName.StartsWith("Khalid") && output.sourceObject != null && output.outputTargetType == typeof(Animator))
                    d.SetGenericBinding(output.sourceObject, anim);
        }
    }

    void SetupHeadLook(Player player)
    {
        Transform model = null;
        foreach (var a in player.GetComponentsInChildren<Animator>()) if (a.name == "MainCharacterAnimated") model = a.transform;
        if (model == null) return;
        headLook = model.GetComponent<CutsceneHeadLook>();
        if (headLook == null) headLook = model.gameObject.AddComponent<CutsceneHeadLook>();
        foreach (var t in model.GetComponentsInChildren<Transform>())
        {
            if (t.name == "neck.x") headLook.neck = t;
            else if (t.name == "head.x") headLook.head = t;
        }
        // he looks at a point that eases from one thing to the next (the coward, the alley, the window)
        if (khalidGaze == null) khalidGaze = new GameObject("KhalidGaze").transform;
        khalidGaze.SetParent(transform, false);
        if (cowardHead != null) khalidGaze.position = cowardHead.position;
        gazeVel = Vector3.zero;
        headLook.target = khalidGaze;
        headLook.facing = player.transform;   // (his model is turned 90 degrees inside the Player)
        headLook.pitchShare = 0.5f;   // he looks down at the (shorter) coward holding his arm
        headLook.weight = 0f; headLook.targetWeight = 0f;

        // and the coward up at Khalid's face
        if (coward == null || cowardHead == null) return;
        cowardHeadLook = coward.GetComponent<CutsceneHeadLook>();
        if (cowardHeadLook == null) cowardHeadLook = coward.AddComponent<CutsceneHeadLook>();
        cowardHeadLook.head = cowardHead;
        cowardHeadLook.neck = cowardHead.parent;
        cowardHeadLook.target = headLook.head;
        cowardHeadLook.pitchShare = 0.5f;
        cowardHeadLook.weight = 0f; cowardHeadLook.targetWeight = 0f;

        SetupArms(model);
    }

    // the coward's hands on Khalid's shoulders (CutsceneArmIK)
    void SetupArms(Transform model)
    {
        Transform K(string n) { foreach (var t in model.GetComponentsInChildren<Transform>()) if (t.name == n) return t; return null; }
        var facing = Player.instance != null ? Player.instance.transform : model;   // the exported model's own axes aren't his facing
        // his rig is flat: the arms (with the gun) and the head sit under the root, not under the chest, so bending the
        // spine has to carry them along by hand
        kSpine = new[] { K("c_spine_01.x"), K("c_spine_02.x"), K("c_spine_03.x") };
        kChest = K("spine_03.x"); kHead = K("head.x");
        kCarry = new[] { K("arm_stretch.l"), K("arm_stretch.r"), K("forearm.l"), K("forearm.r"), K("forearm_stretch.l"), K("forearm_stretch.r"), K("head_scale_fix.x") };
        kArmTwistL = K("arm_twist.l"); kArmTwistR = K("arm_twist.r");
        if (coward == null) return;
        var anim = coward.GetComponent<Animator>();
        cSpine = new[] { anim.GetBoneTransform(HumanBodyBones.Spine), anim.GetBoneTransform(HumanBodyBones.Chest), anim.GetBoneTransform(HumanBodyBones.UpperChest) };
        cHead = anim.GetBoneTransform(HumanBodyBones.Head);
        cHips = anim.GetBoneTransform(HumanBodyBones.Hips);
        cPose = new HumanPoseHandler(anim.avatar, anim.transform);
        cTremble = new[] { anim.GetBoneTransform(HumanBodyBones.LeftLowerArm), anim.GetBoneTransform(HumanBodyBones.RightLowerArm),
                           anim.GetBoneTransform(HumanBodyBones.LeftHand), anim.GetBoneTransform(HumanBodyBones.RightHand) };
        var iks = coward.GetComponents<CutsceneArmIK>();
        cowardGrab = iks.Length > 0 ? iks[0] : coward.AddComponent<CutsceneArmIK>();
        cowardHandsUp = iks.Length > 1 ? iks[1] : coward.AddComponent<CutsceneArmIK>();
        cowardGrab.space = coward.transform;
        // facing each other: his left hand on Khalid's right shoulder; the wrist a little above and in front of the joint
        foreach (var left in new[] { true, false })
        {
            var arm = left ? cowardGrab.left : cowardGrab.right;
            arm.upper = anim.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            arm.lower = anim.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            arm.hand = anim.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            arm.target = K(left ? "arm_twist.r" : "arm_twist.l");   // his shoulder joints
            arm.targetSpace = facing;
            arm.targetOffset = grabOffset;
            arm.elbowHint = new Vector3(left ? -0.35f : 0.35f, -0.5f, 0f);   // elbows bent down and out
            arm.fingerRef = anim.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            arm.thumbRef = anim.GetBoneTransform(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            arm.thumbDirection = new Vector3(left ? 1f : -1f, 0.2f, 0f);         // thumbs in toward his collar
            var curl = new System.Collections.Generic.List<Transform>();
            foreach (var f in left
                ? new[] { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
                          HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
                          HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
                          HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal }
                : new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
                          HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
                          HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
                          HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal })
            { var b = anim.GetBoneTransform(f); if (b != null) curl.Add(b); }
            arm.curlBones = curl.ToArray();
        }
        cowardGrab.weight = 0f; cowardGrab.targetWeight = 0f;

        // hands up at gunpoint ("don't shoot"): wrists at head height, a bit wider than his shoulders and in front of
        // him, elbows down at his sides, fingers straight up, palms flat toward Khalid
        cowardHandsUp.space = coward.transform;
        cowardHandsUp.fingerDirection = new Vector3(0f, 1f, 0.15f);
        cowardHandsUp.palmDirection = Vector3.forward;
        cowardHandsUp.curl = 0f;   // fingers straight, spread
        cowardHandsUp.smoothTime = 0.01f;
        foreach (var left in new[] { true, false })
        {
            var g = left ? cowardGrab.left : cowardGrab.right;
            var arm = left ? cowardHandsUp.left : cowardHandsUp.right;
            arm.upper = g.upper; arm.lower = g.lower; arm.hand = g.hand;
            arm.fingerRef = g.fingerRef; arm.thumbRef = g.thumbRef; arm.curlBones = g.curlBones;
            arm.target = g.upper;   // from his own shoulder joint
            arm.targetSpace = coward.transform;
            arm.elbowHint = new Vector3(left ? -0.35f : 0.35f, -0.7f, 0f);    // elbows down by his sides
            arm.thumbDirection = new Vector3(left ? 1f : -1f, 0f, 0f);         // thumbs in: palms face forward
        }
        cowardHandsUp.weight = 0f; cowardHandsUp.targetWeight = 0f;
    }

    IEnumerator Co_Run()
    {
        var order = new List<(PlayableDirector d, int phase)>
        {
            (open, 0),
            PlaysB ? (versionB, 2) : (versionA, 1),
            (end, 3),
        };

        // fade to black from gameplay, then fade in on the first shot
        var fade = FadeScreenUI.instance;
        if (fade != null && startFade > 0f)
        {
            fade.FadeOut(startFade);
            yield return new WaitForSecondsRealtime(startFade + 0.1f);
        }
        // (switched on only now, so the brain doesn't cut to one of them under the fade-out)
        if (camerasRoot != null) camerasRoot.SetActive(true);

        PlayableDirector previous = null;
        foreach (var (d, phase) in order)
        {
            if (d == null) continue;
            current = d; currentPhase = phase;
            d.time = 0; d.Play(); d.Evaluate();
            if (previous != null) previous.Stop();     // after the next one has posed everything, so nothing pops for a frame
            else if (fade != null && startFade > 0f) fade.FadeIn(startFade * 1.6f);
            previous = d;
            while (!skipping && d.time < d.duration - 0.001) yield return null;
            if (skipping) yield break;
        }
        Finish(false);
    }

    void Finish(bool skipped)
    {
        StoryFlags.Set(DoneFlag);

        float walkPhase = skipped ? -1f : WalkPhaseNow(current);
        foreach (var d in Directors()) if (d != null && d.state == PlayState.Playing) d.Stop();
        HandOverAnimation(walkPhase);
        foreach (var r in subtitleReceivers) if (r != null) r.CloseNow();
        if (coward != null) coward.SetActive(false);
        if (camerasRoot != null) camerasRoot.SetActive(false);
        ApplyAftermath();

        var player = Player.instance;
        if (player != null)
        {
            if (skipped && skipKhalidMark != null)
                player.transform.SetPositionAndRotation(new Vector3(skipKhalidMark.position.x, player.transform.position.y, skipKhalidMark.position.z),
                    Quaternion.Euler(0f, skipKhalidMark.eulerAngles.y, 0f));
            if (skipped) player.OnPlayerTelported?.Invoke();   // gameplay camera straight behind him, under the fade
            if (player.controller != null) player.controller.enabled = true;
            if (player.health != null) player.health.Invincible = false;
        }
        foreach (var b in disabledPlayerBehaviours) if (b != null) b.enabled = true;
        disabledPlayerBehaviours.Clear();
        if (headLook != null) { headLook.targetWeight = 0f; headLook.weight = 0f; }
        if (cowardHeadLook != null) { cowardHeadLook.targetWeight = 0f; cowardHeadLook.weight = 0f; }
        if (cowardGrab != null) { cowardGrab.targetWeight = 0f; cowardGrab.weight = 0f; }
        if (cowardHandsUp != null) { cowardHandsUp.targetWeight = 0f; cowardHandsUp.weight = 0f; }
        tug = 0f; torsoPitch = torsoRoll = headPitch = headRoll = armSwing = default;
        if (AmbienceHandler.Instance != null) AmbienceHandler.Instance.UpdateAmbienceVolume(1f);
        if (CameraSystem.Instance != null) CameraSystem.Instance.ForceShowPlayer = false;
        if (CutsceneSkipper.Instance != null) CutsceneSkipper.Instance.HideHint();
        skipHeld = 0f;

        running = false; IsPlaying = false; current = null; currentPhase = -1;

        // a checkpoint where the meeting leaves him: the done flag only reached disk at the next checkpoint / manual save,
        // so quitting before that and continuing (back at the knife handle) played the meeting again
        if (CheckpointManager.instance != null) CheckpointManager.instance.TriggerCheckpoint("Coward meeting");
    }

    // Where in his walk cycle the timeline has Khalid now (0-1), or -1 if he isn't walking.
    float WalkPhaseNow(PlayableDirector d)
    {
        if (d == null || khalidAnim == null || !(d.playableAsset is TimelineAsset tl)) return -1f;
        double t = d.time;
        foreach (var track in tl.GetOutputTracks())
        {
            if (!(track is AnimationTrack) || d.GetGenericBinding(track) != khalidAnim) continue;
            foreach (var c in track.GetClips())
                if (t >= c.start && t <= c.end + 0.05 && c.animationClip != null && c.animationClip.name == WalkClipName)
                    return Mathf.Repeat((float)c.ToLocalTime(Math.Min(t, c.end - 0.0001)) / c.animationClip.length, 1f);
        }
        return -1f;
    }

    // His Animator Controller takes over from the timeline in the pose the timeline left him in. It sat where it was
    // left when the cutscene began, so he snapped back to that and his stride restarted. Still walking: its walk state
    // on the same clip at the same point of the stride (holding W walks straight on, letting go eases into the idle).
    // Otherwise (a skip, under the fade) standing.
    const string WalkClipName = "Walk_SH";

    void HandOverAnimation(float walkPhase)
    {
        if (khalidAnim == null) return;
        bool walking = walkPhase >= 0f;
        khalidAnim.SetFloat("x", 0f);
        khalidAnim.SetFloat("y", walking ? 1f : 0f);
        khalidAnim.SetFloat("Velocity", walking ? 1f : 0f);
        khalidAnim.SetBool("Turning", false);
        khalidAnim.Play("WalkMove", 0, walking ? Mathf.Repeat(walkPhase - walkCycleOffset, 1f) : 0f);
        khalidAnim.Update(0f);
    }

    void OnDestroy() => cPose?.Dispose();

    // ───────────────────────── per frame ─────────────────────────

    float lastAmbience = 1f;

    void Update()
    {
        if (!running || skipping || current == null || currentPhase < 0 || currentPhase >= phases.Length) return;
        var phase = phases[currentPhase];
        // the director evaluates after Update: its time here is last frame's. Use the time it is about to reach, so on a
        // cut Khalid is already where the new shot expects him (else the walk cam's first frame framed him at the old spot)
        float t = (float)current.time + (current.timeUpdateMode == DirectorUpdateMode.GameTime ? Time.deltaTime : Time.unscaledDeltaTime);

        var player = Player.instance;
        if (player != null && phase.khalid != null && phase.khalid.Length > 0)
        {
            Evaluate(phase.khalid, t, true, true, out var p, out var yaw);
            Vector3 was = player.transform.position, now = new Vector3(p.x, startPos.y, p.z);
            player.transform.SetPositionAndRotation(now, Quaternion.Euler(0f, yaw, 0f));
            // a jump on a cut (into the alley on "Thanks."): the cameras following him start there, no swoop. He turns
            // too, and the walk cam damps in his local space, so a warp (position only) still swung it round behind
            // him - forget their damping history instead: the first frame of the shot is exactly in place.
            if ((now - was).sqrMagnitude > 1f && camerasRoot != null)
                foreach (var vc in camerasRoot.GetComponentsInChildren<CinemachineVirtualCameraBase>())
                    vc.PreviousStateIsValid = false;
        }
        if (headLook != null && phase.headLook != null && phase.headLook.Length > 0)
            headLook.targetWeight = Step(phase.headLook, t);
        if (khalidGaze != null)
        {
            int at = phase.headLookAt != null && phase.headLookAt.Length > 0 ? Mathf.RoundToInt(Step(phase.headLookAt, t)) : -1;
            Transform look = at >= 0 && at < lookPoints.Length ? lookPoints[at] : cowardHead;
            if (look != null) khalidGaze.position = Vector3.SmoothDamp(khalidGaze.position, look.position, ref gazeVel, 0.3f);
            // at the coward he mostly turns his head; at something (the alley, the window) he tips it right up or down to it
            if (headLook != null) headLook.pitchShare = at >= 0 ? 0.9f : 0.5f;
        }
        if (cowardHeadLook != null)
            cowardHeadLook.targetWeight = phase.cowardLook != null && phase.cowardLook.Length > 0 ? Step(phase.cowardLook, t) : 0f;
        if (cowardGrab != null)
        {
            cowardGrab.targetWeight = phase.grab != null && phase.grab.Length > 0 ? Step(phase.grab, t) : 0f;
            float tugTarget = phase.tug != null && phase.tug.Length > 0 ? Step(phase.tug, t) : 0f;
            tug = Mathf.MoveTowards(tug, tugTarget, Time.deltaTime * 0.05f);
        }
        // the gameplay camera kept the angle it had when the scene started; put it behind Khalid (unseen, under the
        // last cutscene shot) so the blend back at the end lands on a normal gameplay view
        if (currentPhase == 3 && !gameplayCameraSnapped && t >= gameplayCameraSnapTime && player != null)
        {
            gameplayCameraSnapped = true;
            player.OnPlayerTelported?.Invoke();
        }
        if (phase.ambience != null && phase.ambience.Length > 0 && AmbienceHandler.Instance != null)
        {
            float a = Step(phase.ambience, t);
            if (!Mathf.Approximately(a, lastAmbience)) { AmbienceHandler.Instance.UpdateAmbienceVolume(a); lastAmbience = a; }
        }

        HandleSkip();
    }

    // The coward's mover is placed after the timeline has evaluated this frame (Update only sees last frame's time: on
    // the first frame of his run he stood at the mover's resting place, the world origin, and the camera aimed there).
    // Runs before the CinemachineBrain's LateUpdate (execution order).
    void LateUpdate()
    {
        if (running && khalidVoice != null && kHead != null) khalidVoice.position = kHead.position;
        ShakeKhalid();
        GunDip();
        PoseLayers();
        TrembleHands();
        if (!running || skipping || current == null || currentPhase < 0 || currentPhase >= phases.Length) return;
        var phase = phases[currentPhase];
        if (cowardMover != null && phase.cowardMover != null && phase.cowardMover.Length > 0)
        {
            Evaluate(phase.cowardMover, (float)current.time, false, false, out var p, out var yaw);   // a run: constant speed
            cowardMover.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
        }
    }

    // Poses the coward is bent into on top of whatever clip plays, in muscle space so they fit any clip. The hips are put
    // back where the clip had them, so his legs and feet don't move.
    // The Mixamo walks swing their arms like a man out for a stroll: on the walk-in he is hunched over, shoulders up,
    // elbows tucked in and his hands clutched together under his chin (the head stays on its looking-round layer).
    static readonly (string muscle, float value)[] ScaredPose =
    {
        ("Spine Front-Back", -0.35f), ("Chest Front-Back", -0.5f), ("UpperChest Front-Back", -0.8f),
        ("Left Shoulder Down-Up", 0.7f), ("Right Shoulder Down-Up", 0.7f),
        ("Left Shoulder Front-Back", -1f), ("Right Shoulder Front-Back", -1f),
        ("Left Arm Down-Up", -0.8f), ("Right Arm Down-Up", -0.8f),
        ("Left Arm Front-Back", -0.3f), ("Right Arm Front-Back", -0.3f),
        ("Left Arm Twist In-Out", -0.2f), ("Right Arm Twist In-Out", -0.2f),
        ("Left Forearm Stretch", -0.6f), ("Right Forearm Stretch", -0.6f),
    };
    // While the gun is on him he shrinks into his shoulders, a little bent over, hands open (fingers straight and a little
    // spread: +1 Stretched is a flat hand); his hands go up by arm IK (HandsUp).
    static readonly (string muscle, float value)[] SurrenderPose = new[]
    {
        ("Spine Front-Back", -0.15f), ("Chest Front-Back", -0.2f),
        ("Left Shoulder Down-Up", 0.4f), ("Right Shoulder Down-Up", 0.4f),
    }.Concat(OpenHands()).ToArray();

    static IEnumerable<(string, float)> OpenHands()
    {
        foreach (var side in new[] { "Left", "Right" })
            foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            {
                for (int j = 1; j <= 3; j++) yield return ($"{side} {finger} {j} Stretched", 1f);
                yield return ($"{side} {finger} Spread", 0.4f);
            }
    }
    static int[] scaredMuscles, surrenderMuscles;

    static int[] MuscleIndices((string muscle, float value)[] pose)
    {
        var idx = new int[pose.Length];
        for (int i = 0; i < pose.Length; i++) idx[i] = System.Array.IndexOf(HumanTrait.MuscleName, pose[i].muscle);
        return idx;
    }

    static float Ramp(float t, float from, float to) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, t));

    void PoseLayers()
    {
        if (!running || cPose == null || coward == null || !coward.activeInHierarchy || current != open || open == null) { HandsUp(0f); return; }
        float t = (float)open.time;
        float scared = scaredWalk * (1f - Ramp(t, scaredWalkEnd - 0.6f, scaredWalkEnd));
        float hands = surrender * Ramp(t, surrenderUp - 0.6f, surrenderUp) * (1f - Ramp(t, surrenderDown, surrenderDown + 0.8f));
        HandsUp(hands);
        if (scared <= 0f && hands <= 0f) return;
        scaredMuscles ??= MuscleIndices(ScaredPose);
        surrenderMuscles ??= MuscleIndices(SurrenderPose);
        Vector3 hipsPos = cHips.position; Quaternion hipsRot = cHips.rotation;
        cPose.GetHumanPose(ref cHumanPose);
        Blend(ScaredPose, scaredMuscles, scared);
        Blend(SurrenderPose, surrenderMuscles, hands);
        cPose.SetHumanPose(ref cHumanPose);
        cHips.SetPositionAndRotation(hipsPos, hipsRot);
    }

    // his wrists beside his head (CutsceneArmIK runs after this), shaking a little in the air
    void HandsUp(float w)
    {
        if (cowardHandsUp == null) return;
        cowardHandsUp.weight = cowardHandsUp.targetWeight = w;
        if (w <= 0f) return;
        float t = Time.time * 7f;
        for (int i = 0; i < 2; i++)
        {
            var arm = i == 0 ? cowardHandsUp.left : cowardHandsUp.right;
            var shake = new Vector3(Mathf.PerlinNoise(t, i * 5.1f) - 0.5f, Mathf.PerlinNoise(i * 2.7f, t) - 0.5f, 0f) * 0.02f;
            arm.targetOffset = new Vector3(i == 0 ? -handsUpOffset.x : handsUpOffset.x, handsUpOffset.y, handsUpOffset.z) + shake;
        }
    }

    void Blend((string muscle, float value)[] pose, int[] idx, float w)
    {
        if (w <= 0f) return;
        for (int i = 0; i < pose.Length; i++)
            if (idx[i] >= 0) cHumanPose.muscles[idx[i]] = Mathf.Lerp(cHumanPose.muscles[idx[i]], pose[i].value, w);
    }

    // the coward's hands shake the whole scene: a fast, uneven tremor on the forearms (a little) and the wrists (more),
    // on top of whatever clip plays; when he holds Khalid the arm IK (after this) takes the hands over
    void TrembleHands()
    {
        if (!running || cTremble == null || coward == null || !coward.activeInHierarchy || handTremble <= 0f) return;
        float t = Time.time * 9f;
        for (int i = 0; i < cTremble.Length; i++)
        {
            var b = cTremble[i];
            if (b == null) continue;
            float amp = handTremble * (i < 2 ? 0.35f : 1f);
            float x = Mathf.PerlinNoise(t, i * 7.3f) * 2f - 1f, y = Mathf.PerlinNoise(i * 3.1f, t * 1.13f) * 2f - 1f, z = Mathf.PerlinNoise(t * 0.87f, 11f + i) * 2f - 1f;
            b.localRotation = Quaternion.Euler(x * amp, y * amp * 0.6f, z * amp) * b.localRotation;
        }
    }

    // damped spring (x follows a target, or is kicked by an acceleration)
    struct Spring
    {
        public float x, v;
        public float Step(float target, float hz, float damping, float kick, float dt)
        {
            float w = 2f * Mathf.PI * hz;
            v += (w * w * (target - x) - 2f * damping * w * v + kick) * dt;
            x += v * dt;
            return x;
        }
    }

    // The coward shakes him by the shoulders: an uneven push-pull (~shakeRate a second, phase `tug` = how far his shoulders
    // go, in metres). Khalid gives at the waist and spine (a springy follow, a little side-to-side too), his head lags and
    // snaps after the chest, his hanging arms swing; his feet stay put. The coward leans into each pull and push, a beat
    // ahead. Runs after the Animator wrote this frame's pose and before the coward's hands are put on the shoulders
    // (CutsceneArmIK) and the head-looks.
    void ShakeKhalid()
    {
        float dt = Time.deltaTime;
        if (!running || kChest == null || dt <= 0f) return;
        float amp = tug * shakeGain;
        if (amp < 0.0005f && Mathf.Abs(torsoPitch.x) + Mathf.Abs(torsoPitch.v) + Mathf.Abs(headPitch.x) + Mathf.Abs(headPitch.v) < 0.01f) return;
        dt = Mathf.Min(dt, 1f / 30f);

        // push-pull: a sine whose speed and strength wander a little (people don't shake like a metronome)
        shakeClock += dt * shakeRate * (0.8f + 0.4f * Mathf.PerlinNoise(Time.time * 0.7f, 3.1f));
        float strength = 0.65f + 0.7f * Mathf.PerlinNoise(Time.time * 1.3f, 8.2f);
        float pull = Mathf.Sin(shakeClock * 2f * Mathf.PI) * strength;                 // + = toward the coward
        float side = Mathf.Sin(shakeClock * 2f * Mathf.PI + 1.3f) * 0.35f * strength;
        const float chestHeight = 0.5f;                                                  // spine base -> shoulders
        float pitchTarget = Mathf.Asin(Mathf.Clamp(amp * pull / chestHeight, -0.5f, 0.5f)) * Mathf.Rad2Deg;
        float rollTarget = Mathf.Asin(Mathf.Clamp(amp * side / chestHeight, -0.5f, 0.5f)) * Mathf.Rad2Deg;

        float v0 = torsoPitch.v, r0 = torsoRoll.v;
        float pitch = torsoPitch.Step(pitchTarget, 3.2f, 0.45f, 0f, dt);
        float roll = torsoRoll.Step(rollTarget, 3.2f, 0.5f, 0f, dt);
        float pitchAcc = (torsoPitch.v - v0) / dt, rollAcc = (torsoRoll.v - r0) / dt;
        // the head and the hanging arms are carried by the chest and lag behind it (kicked the other way by its acceleration)
        float hp = Mathf.Clamp(headPitch.Step(0f, 2.4f, 0.35f, -0.9f * pitchAcc, dt), -18f, 18f);
        float hr = Mathf.Clamp(headRoll.Step(0f, 2.4f, 0.4f, -0.7f * rollAcc, dt), -12f, 12f);
        float arm = Mathf.Clamp(armSwing.Step(0f, 1.6f, 0.3f, -0.8f * pitchAcc, dt), -20f, 20f);

        var body = Player.instance != null ? Player.instance.transform : kChest;
        Vector3 fwd = body.forward, right = body.right;

        // spine, spread over its three joints; the flat-rigged arms + head follow the chest
        Vector3 chestPos = kChest.position; Quaternion chestRot = kChest.rotation;
        float[] share = { 0.3f, 0.35f, 0.35f };
        for (int i = 0; i < kSpine.Length; i++)
            if (kSpine[i] != null) kSpine[i].rotation = Quaternion.AngleAxis(pitch * share[i], right) * Quaternion.AngleAxis(roll * share[i], fwd) * kSpine[i].rotation;
        Quaternion dq = kChest.rotation * Quaternion.Inverse(chestRot);
        foreach (var b in kCarry)
            if (b != null) b.SetPositionAndRotation(kChest.position + dq * (b.position - chestPos), dq * b.rotation);

        if (kHead != null) kHead.rotation = Quaternion.AngleAxis(hp, right) * Quaternion.AngleAxis(hr, fwd) * kHead.rotation;

        // arms swing from the shoulders (upper-arm twist bones carry part of the sleeve and turn ~90% with the arm)
        foreach (var (twist, bones) in new[] { (kArmTwistL, new[] { kCarry[0], kCarry[2], kCarry[4] }), (kArmTwistR, new[] { kCarry[1], kCarry[3], kCarry[5] }) })
        {
            if (twist == null) continue;
            Vector3 pivot = twist.position; Quaternion q = Quaternion.AngleAxis(arm, right);
            foreach (var b in bones) if (b != null) b.SetPositionAndRotation(pivot + q * (b.position - pivot), q * b.rotation);
            twist.rotation = Quaternion.Slerp(Quaternion.identity, q, 0.9f) * twist.rotation;
        }

        // the coward leans back into each pull and forward into each push, ahead of Khalid (he's the one doing it)
        if (cSpine != null && coward != null)
        {
            Transform c = coward.transform;
            float cp = -0.45f * pitchTarget, cr = -0.4f * rollTarget;
            float[] cshare = { 0.35f, 0.35f, 0.3f };
            for (int i = 0; i < cSpine.Length; i++)
                if (cSpine[i] != null) cSpine[i].rotation = Quaternion.AngleAxis(cp * cshare[i], c.right) * Quaternion.AngleAxis(cr * cshare[i], c.forward) * cSpine[i].rotation;
            if (cHead != null) cHead.rotation = Quaternion.AngleAxis(-0.5f * hp, c.right) * cHead.rotation;
        }
    }

    // Khalid lowers his aimed gun a little: both arms (and the gun in his hands) tip down together about the line through
    // his shoulders, so the grip stays as the clip has it. The twist bones carry part of the sleeve.
    void GunDip()
    {
        if (!running || current == null || currentPhase < 0 || currentPhase >= phases.Length || kCarry == null || kArmTwistL == null || kArmTwistR == null) return;
        var keys = phases[currentPhase].gunDip;
        if (keys == null || keys.Length == 0) return;
        float angle = Lerp(keys, (float)current.time) * gunDipAngle;
        if (Mathf.Abs(angle) < 0.01f) return;
        var body = Player.instance != null ? Player.instance.transform : kChest;
        Quaternion q = Quaternion.AngleAxis(angle, body.right);
        foreach (var (twist, bones) in new[] { (kArmTwistL, new[] { kCarry[0], kCarry[2], kCarry[4] }), (kArmTwistR, new[] { kCarry[1], kCarry[3], kCarry[5] }) })
        {
            Vector3 pivot = twist.position;
            foreach (var b in bones) if (b != null) b.SetPositionAndRotation(pivot + q * (b.position - pivot), q * b.rotation);
            twist.rotation = Quaternion.Slerp(Quaternion.identity, q, 0.9f) * twist.rotation;
        }
    }

    // straight lines between (time, value) keys
    static float Lerp(Vector2[] keys, float t)
    {
        if (t <= keys[0].x) return keys[0].y;
        for (int i = 1; i < keys.Length; i++)
            if (t <= keys[i].x) return Mathf.Lerp(keys[i - 1].y, keys[i].y, Mathf.InverseLerp(keys[i - 1].x, keys[i].x, t));
        return keys[keys.Length - 1].y;
    }

    // between pose keys: eased in/out (smooth) or at constant speed
    void Evaluate(PoseKey[] keys, float t, bool allowCurrent, bool smooth, out Vector3 pos, out float yaw)
    {
        PoseKey Get(int i)
        {
            var k = keys[i];
            if (allowCurrent && k.fromCurrent) { k.position = startPos; k.yaw = startYaw; }
            return k;
        }
        if (t <= keys[0].time || keys.Length == 1) { var k0 = Get(0); pos = k0.position; yaw = k0.yaw; return; }
        for (int i = 1; i < keys.Length; i++)
        {
            if (t > keys[i].time) continue;
            var a = Get(i - 1); var b = Get(i);
            if (b.cut && t < b.time) { pos = a.position; yaw = a.yaw; return; }
            float u = Mathf.InverseLerp(a.time, b.time, t);
            if (smooth && !b.linear) u = u * u * (3f - 2f * u);
            pos = Vector3.Lerp(a.position, b.position, u);
            yaw = Mathf.LerpAngle(a.yaw, b.yaw, u);
            return;
        }
        var last = Get(keys.Length - 1); pos = last.position; yaw = last.yaw;
    }

    // hold-the-last-value steps (x = time, y = value)
    static float Step(Vector2[] keys, float t)
    {
        float v = keys[0].y;
        foreach (var k in keys) if (t >= k.x) v = k.y;
        return v;
    }

    // ───────────────────────── skipping ─────────────────────────

    void HandleSkip()
    {
        // hold E, same as the game's other cutscenes, and shown with their prompt (the scene's CutsceneSkipper: it
        // shows up for a moment whenever a key is pressed, so it never covers the scene for long)
        var prompt = CutsceneSkipper.Instance;
        float holdTime = prompt != null ? prompt.HoldDuration : skipHoldTime;
        bool held = (Keyboard.current != null && Keyboard.current.eKey.isPressed)
                    || (Gamepad.current != null && Gamepad.current.startButton.isPressed);
        if (held) skipHeld += Time.unscaledDeltaTime;
        else skipHeld = Mathf.Max(0f, skipHeld - Time.unscaledDeltaTime * 2f);

        if (prompt != null)
        {
            if (held || (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)) prompt.ShowHint();
            prompt.SetHoldProgress(skipHeld / holdTime);
        }

        if (skipHeld >= holdTime) StartCoroutine(Co_Skip());
    }

    IEnumerator Co_Skip()
    {
        skipping = true;
        if (FadeScreenUI.instance != null) FadeScreenUI.instance.FadeOut(0.35f);
        yield return new WaitForSecondsRealtime(0.4f);
        Finish(true);
        skipping = false;
        yield return null;
        if (FadeScreenUI.instance != null) FadeScreenUI.instance.FadeIn(0.5f);
    }
}
