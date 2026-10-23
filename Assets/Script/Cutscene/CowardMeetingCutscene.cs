using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

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
        [Tooltip("x = time, y = head-look weight of the coward toward Khalid's face (while he pleads holding him).")]
        public Vector2[] cowardLook = new Vector2[0];
        [Tooltip("x = time, y = weight of the coward's hands on Khalid's shoulders.")]
        public Vector2[] grab = new Vector2[0];
        [Tooltip("x = time, y = how hard (metres) he tugs at Khalid's shoulders while holding them.")]
        public Vector2[] tug = new Vector2[0];
        [Tooltip("x = time, y = ambience volume multiplier (1 = normal, 0.71 = -3 dB, 0.5 = -6 dB).")]
        public Vector2[] ambience = new Vector2[0];
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

    [Header("Conditions")]
    public Item knifeHandle;

    [Header("Actors")]
    public GameObject coward;
    public Transform cowardMover;
    public Transform cowardHead;
    [Tooltip("Enemies under this root that are still alive when the scene starts are despawned (Alley 1).")]
    public Transform alleyEnemiesRoot;
    public float despawnRadius = 60f;

    [Tooltip("The cutscene's virtual cameras: only switched on while the scene plays, so they never compete with the gameplay camera.")]
    public GameObject camerasRoot;

    [Header("Where Khalid is left after a skip")]
    public Transform skipKhalidMark;

    [Header("Left as the scene leaves it (the chair he knocks over)")]
    public Transform tippedChairPivot;
    public Vector3 tippedChairLocalPosition;
    public Vector3 tippedChairLocalEuler;

    [Tooltip("End timeline time at which the (hidden) gameplay camera is snapped behind Khalid, before the blend back.")]
    public float gameplayCameraSnapTime = 11.5f;

    [Header("Skip")]
    public float skipHoldTime = 1f;
    public CanvasGroup skipPrompt;
    public Image skipFill;
    public CutsceneSubtitleReceiver[] subtitleReceivers;

    [Tooltip("Where the coward's wrists go on Khalid's shoulders: from each shoulder joint, in Khalid's space (x right, y up, z forward).")]
    public Vector3 grabOffset = new Vector3(0f, 0.06f, 0.13f);

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
    float skipHeld, promptShowTime;
    Vector3 startPos; float startYaw;
    CutsceneHeadLook headLook, cowardHeadLook;
    CutsceneArmIK cowardGrab;
    Transform khalidModel; Quaternion khalidModelRot; bool rocking; float tug;
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
        if (skipPrompt != null) skipPrompt.alpha = 0f;
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
        foreach (var g in debugHide) if (g != null) g.SetActive(false);
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

    // the timelines' "Khalid" animation tracks follow whatever player object exists at runtime
    void BindKhalid(Player player)
    {
        Animator anim = null;
        foreach (var a in player.GetComponentsInChildren<Animator>())
            if (a.name == "MainCharacterAnimated") { anim = a; break; }
        if (anim == null) return;
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
        headLook.target = cowardHead;
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
        khalidModel = model;
        if (coward == null) return;
        var anim = coward.GetComponent<Animator>();
        cowardGrab = coward.GetComponent<CutsceneArmIK>();
        if (cowardGrab == null) cowardGrab = coward.AddComponent<CutsceneArmIK>();
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
    }

    IEnumerator Co_Run()
    {
        var order = new List<(PlayableDirector d, int phase)>
        {
            (open, 0),
            versionBEnabled && StoryFlags.Has(Alley2Flag) ? (versionB, 2) : (versionA, 1),
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

        foreach (var d in Directors()) if (d != null && d.state == PlayState.Playing) d.Stop();
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
        tug = 0f; RockKhalid();
        if (AmbienceHandler.Instance != null) AmbienceHandler.Instance.UpdateAmbienceVolume(1f);
        if (CameraSystem.Instance != null) CameraSystem.Instance.ForceShowPlayer = false;
        if (skipPrompt != null) skipPrompt.alpha = 0f;

        running = false; IsPlaying = false; current = null; currentPhase = -1;
    }

    // ───────────────────────── per frame ─────────────────────────

    float lastAmbience = 1f;

    void Update()
    {
        if (!running || skipping || current == null || currentPhase < 0 || currentPhase >= phases.Length) return;
        var phase = phases[currentPhase];
        float t = (float)current.time;

        var player = Player.instance;
        if (player != null && phase.khalid != null && phase.khalid.Length > 0)
        {
            Evaluate(phase.khalid, t, true, true, out var p, out var yaw);
            player.transform.SetPositionAndRotation(new Vector3(p.x, startPos.y, p.z), Quaternion.Euler(0f, yaw, 0f));
        }
        if (headLook != null && phase.headLook != null && phase.headLook.Length > 0)
            headLook.targetWeight = Step(phase.headLook, t);
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
        RockKhalid();
        if (!running || skipping || current == null || currentPhase < 0 || currentPhase >= phases.Length) return;
        var phase = phases[currentPhase];
        if (cowardMover != null && phase.cowardMover != null && phase.cowardMover.Length > 0)
        {
            Evaluate(phase.cowardMover, (float)current.time, false, false, out var p, out var yaw);   // a run: constant speed
            cowardMover.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
        }
    }

    // The coward shakes him: Khalid's whole model rocks a little about his feet, toward the coward and back, in uneven
    // pulls (noise, not a sine); the coward's hands follow his shoulders (CutsceneArmIK runs after this).
    void RockKhalid()
    {
        if (khalidModel == null) return;
        if (!running || tug <= 0.0001f) { if (rocking) { khalidModel.localRotation = khalidModelRot; rocking = false; } return; }
        if (!rocking) { khalidModelRot = khalidModel.localRotation; rocking = true; }
        float n = Mathf.PerlinNoise(Time.time * 4.5f, 0.37f) * 2f - 1f;
        float degrees = n * tug / 1.65f * Mathf.Rad2Deg;          // tug = metres at shoulder height
        khalidModel.localRotation = Quaternion.AngleAxis(degrees, Vector3.right) * khalidModelRot;
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
            float u = Mathf.InverseLerp(a.time, b.time, t);
            if (smooth) u = u * u * (3f - 2f * u);
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
        // hold E, same as the game's other cutscenes (CutsceneSkipper)
        bool held = (Keyboard.current != null && Keyboard.current.eKey.isPressed)
                    || (Gamepad.current != null && Gamepad.current.startButton.isPressed);
        if (held) { skipHeld += Time.unscaledDeltaTime; promptShowTime = 2f; }
        else skipHeld = Mathf.Max(0f, skipHeld - Time.unscaledDeltaTime * 2f);

        // the prompt shows up for a moment whenever a key is pressed, so it never covers the scene for long
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) promptShowTime = 2f;
        promptShowTime -= Time.unscaledDeltaTime;
        if (skipPrompt != null) skipPrompt.alpha = Mathf.MoveTowards(skipPrompt.alpha, promptShowTime > 0f ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        if (skipFill != null) skipFill.fillAmount = Mathf.Clamp01(skipHeld / skipHoldTime);

        if (skipHeld >= skipHoldTime) StartCoroutine(Co_Skip());
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
