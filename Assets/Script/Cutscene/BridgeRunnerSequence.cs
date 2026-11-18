using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Localization;

/// <summary>
/// After the knife handle (Alley 1), on the way north (Game scene). Khalid walks into the user's drawn area and the Runner,
/// Adel still over its shoulder, bursts out from behind the parked car nearest to him on the main road.
///
/// 1. Mini cutscene (~3 s, user 2026-11-18: "a mini cutscene shows him running and then give player control back and chase
///    starts"): Khalid stops, the view eases over his shoulder onto the car, the Runner comes out from behind it and runs.
/// 2. Chase, like the park chase (KidnapSequence): Khalid has control, fast chase run, no pipe or gun; the Runner keeps
///    ~10 m ahead along the main road and up the side road, waits for him in the fog if he falls behind, and the view
///    swings to it with "I better chase him!" if he hangs back or wanders off. The table-knockdown chase music plays.
/// 3. Ending (the plank "thrown in the ravine"): the Runner sprints over the plank bridge; once Khalid is close it turns,
///    shoves the plank down into the ravine, looks back across at him and is gone into the fog. Khalid stops and the view
///    eases to the ravine's near edge for it, then back to him. Hold E skips that part.
///
/// No voice for the Runner here (user), only its footsteps. Saves: plays once. The done flag (StoryFlags, kept through a
/// death rolling the save back) is set when it starts, and a checkpoint is written when Khalid has control again at the end.
/// The plank is on the bridge only between the knife handle pickup and this; afterwards it lies in the ravine (also after a
/// reload). Built by Tools > Cutscenes > Build Bridge Runner Sequence.
/// </summary>
[DefaultExecutionOrder(150)]
public class BridgeRunnerSequence : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }
    public const string DoneFlag = "BridgeRunnerDone";
    public const string CheckpointName = "Runner dropped the bridge";

    [Header("When (user: after the knife handle, walking into the drawn area)")]
    [Tooltip("UniqueID of the knife handle pickup: armed once it has been picked up.")]
    [SerializeField] string knifeHandleId;
    [Tooltip("UniqueID of BrokenBridgeInteractable: once Khalid has laid his own plank there this is long over.")]
    [SerializeField] string bridgeRepairId;
    [Tooltip("The area (world box, no collider so camera rays never hit it): Khalid inside it -> it plays.")]
    // near edge x -120 (user 2026-11-18: moved up 11 m, just short of the road) to x -89.3
    [SerializeField] Vector3 areaCenter = new Vector3(-104.65f, 4f, -23.5f);
    [SerializeField] Vector3 areaSize = new Vector3(30.7f, 20f, 97f);
    [Tooltip("Safety net: Khalid further north than this (x) while it is still waiting = he got round it; it's marked done quietly.")]
    [SerializeField] float northOfRoadX = -100f;

    [Header("Actors")]
    [SerializeField] RunnerPuppet runner;
    [SerializeField] Animator adel;                  // Adel_Kidnap.controller (Idle / Walk / Run)
    [Tooltip("The plank across the ravine (copy of the bridge's own repair plank).")]
    [SerializeField] Transform plank;

    /// <summary>Where the Runner waits: crouched behind a parked car on the main road (user: "make the runner comes from
    /// something instead of randomly spawning"). It comes out round the car when it all starts.</summary>
    [System.Serializable]
    public class CoverSpot
    {
        [Tooltip("Where it crouches, on the far side of the car from Khalid.")]
        public Vector3 hide;
        [Tooltip("The car it hides behind (the over-the-shoulder camera may look across it).")]
        public Transform cover;
        [Tooltip("How far down it starts (crouched): with Adel on its shoulder it is taller than the cars.")]
        public float crouch = 1.1f;
        [Tooltip("Out from behind the car and along the main road to the side road (then 'route').")]
        public List<Vector3> leadIn = new List<Vector3>();
    }

    [Header("Route (world; y is found on the ground)")]
    [Tooltip("The Runner's hiding spots behind parked cars, by the user's R marks. It starts at the one nearest Khalid.")]
    [SerializeField] List<CoverSpot> spawns = new List<CoverSpot>();
    [Tooltip("From the side road, over the plank, to where it stands to shove it (last point).")]
    [SerializeField] List<Vector3> route = new List<Vector3>();
    [Tooltip("Off north after the shove (gone at the end).")]
    [SerializeField] List<Vector3> away = new List<Vector3>();
    [Tooltip("Ground speed its run clip matches at animator speed 1.")]
    [SerializeField] float runnerClipSpeed = 3.2f;

    [Header("Mini cutscene at the start")]
    [Tooltip("Runner speed while the camera is on it at the start.")]
    [SerializeField] float introRunSpeed = 4.6f;
    [Tooltip("The view goes back to Khalid and the chase starts at this time.")]
    [SerializeField] float introSeconds = 2.9f;

    [Header("Chase (like the park chase)")]
    [Tooltip("Khalid's sprint while chasing (normal sprint 3.2). The locomotion tree switches to the fast mocap run above 3.25.")]
    [SerializeField] float chaseSprintSpeed = 3.8f;
    [SerializeField] float gapWanted = 9.5f;
    [Tooltip("It never stops while Khalid could still see it (user): falling behind, it only eases off to this.")]
    [SerializeField] float runnerMinSpeed = 3.6f, runnerMaxSpeed = 5.5f;
    [Tooltip("Lost in the fog: further than this from the camera it can't be seen, so it may wait for Khalid there.")]
    [SerializeField] float sightDistance = 22f;
    [Tooltip("Off screen it may also wait, but only this far ahead of Khalid or more.")]
    [SerializeField] float offScreenRestDistance = 14f;
    [Tooltip("Past this x (the side road, just short of the ravine) it sprints flat out for the plank: Khalid never gets onto it.")]
    [SerializeField] float dashFromX = -96f;
    [Tooltip("It shoves the plank in once Khalid is this close (m, straight line) - waiting on the far side until then.")]
    [SerializeField] float shoveWhenWithin = 19f;
    [SerializeField] float escapeSpeed = 5.4f;

    [Header("Falling behind (as in the park chase)")]
    [SerializeField] float hintAfterWaiting = 3.5f;
    [SerializeField] float hintSeconds = 2.4f, hintCooldown = 15f;
    [SerializeField] string hintLine = "I better chase him!";
    [Tooltip("Khalid this far from it (straight line) for farSeconds -> the reminder (while it's still in sight, running away).")]
    [SerializeField] float farDistance = 14f, farSeconds = 1.2f, farCooldown = 6f, leashDistance = 24f, lineCooldown = 10f;

    [Header("Fog (user: more foggy while it lasts)")]
    [Tooltip("Enviro height fog density during the chase (Game scene normally 0.7; the park chase uses 1.35).")]
    [SerializeField] float chaseFogDensity = 1.2f;
    [Tooltip("For the plank going in: still thicker than normal, but readable.")]
    [SerializeField] float endingFogDensity = 0.95f;
    [SerializeField] float fogRampIn = 3f, fogRampOut = 6f;

    [Header("Plank going down")]
    [Tooltip("Slides this far toward the near side (shoved from the far end) before its far end drops off the edge.")]
    [SerializeField] float slideDistance = 1.5f;
    [SerializeField] float slideSeconds = 0.55f, fallSeconds = 1.0f;
    [Tooltip("Where it ends up: wedged diagonally in the ravine, far end at the bottom.")]
    [SerializeField] Vector3 fallenPos = new Vector3(-86.6f, -1.5f, -8.3f);
    [SerializeField] float fallenTilt = 52f, fallenYaw = 7f;

    [Header("Camera")]
    [Tooltip("Back to Khalid after the ending: a quick dip through black if skipped.")]
    [SerializeField] float backFadeOut = 0.4f, backFadeIn = 0.6f;
    [SerializeField] float followBack = 5.5f, followUp = 2.3f, followFov = 50f;
    [Tooltip("The ending's camera never goes past the near edge of the ravine (x).")]
    [SerializeField] float cameraMaxX = -92f;

    [Header("Sound")]
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField] AudioClip plankCreak, plankDrop;
    [Tooltip("Its footsteps (1 = Khalid's own loudness).")]
    [SerializeField] float footstepVolume = 1.7f;
    [Header("Music (the park chase's)")]
    [SerializeField] AudioClip music;
    [SerializeField] AudioMixerGroup musicGroup;
    [SerializeField] float musicVolume = 0.5f;

    // ---- runner beats at the plank (seconds)
    const float TurnTime = 0.35f, ShoveHit = 0.3f, ShoveEnd = 1.0f, StareEnd = 1.7f, BackAfterLeaving = 1.3f;
    const float RunAfter = 1.2f;    // crouched behind the car until this, then out

    enum Phase { Waiting, Intro, Chase, Ending, Done }
    enum Beat { Hidden, Running, Waiting, AtPlank, Turning, Shoving, Staring, Leaving, Gone }
    Phase phase = Phase.Waiting;
    Beat beat;
    float t, beatT, skipHeld;
    bool skipping, returning;

    readonly List<Vector3> pts = new List<Vector3>();
    readonly List<float> cum = new List<float>();
    float runnerS, runnerY, shoveS, playerS;
    CoverSpot spot;
    float crouchNow;

    // plank
    Vector3 plankP0; Quaternion plankR0;
    float plankT = -1f;
    bool dropPlayed;
    enum PlankState { Unset, Hidden, OnBridge, Fallen, Falling }
    PlankState plankState = PlankState.Unset;
    Collider[] plankColliders;

    // camera
    CinemachineVirtualCamera noticeCam, followCam;
    CinemachineBrain brain;
    CinemachineBlendDefinition savedBlend;
    bool blendSaved;
    float restoreBlendAt = -1f;
    Vector3 noticeOffset, followVel;
    float noticeStart;

    // khalid
    readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();
    bool pausedMovement, madeInvincible, turnKhalid;
    float savedSprint; bool savedStamina, savedWeapon, sprintChanged;
    PlayerWeaponEnabler gunEnabler;
    // falling behind
    Vector3 lastPlayerPos;
    float lastPlayerS, chaseEffort;
    float waitingTime, farTime, lastHint = -999f, lastLine = -999f, hintUntil = -1f;
    bool pausedByHint;

    // fog (absolute densities; fogNow < 0 = not ours)
    float fogBase = -1f, fogNow = -1f, fogGoal = -1f;

    // adel / sound
    Transform adelHips, runnerShoulder;
    float adelHipHeight = 0.6f;
    AudioSource sfx, musicSource;
    ScriptedFootsteps steps;
    readonly List<AudioSource> pausedMusic = new List<AudioSource>();
    readonly Dictionary<AudioSource, float> pausedVolume = new Dictionary<AudioSource, float>();

    // ------------------------------------------------------------------ setup

    void Awake()
    {
        if (runner != null) foreach (var tr in runner.GetComponentsInChildren<Transform>(true)) if (tr.name == "RightShoulder") runnerShoulder = tr;
        if (adel != null)
        {
            foreach (var tr in adel.GetComponentsInChildren<Transform>(true)) if (tr.name == "Hips") adelHips = tr;
            adel.applyRootMotion = false;
            adel.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        if (plank != null)
        {
            plankP0 = plank.position; plankR0 = plank.rotation;
            plankColliders = plank.GetComponentsInChildren<Collider>(true);
        }

        sfx = NewSource("Bridge Runner Audio", sfxGroup, 0.35f, false);
        sfx.rolloffMode = AudioRolloffMode.Linear; sfx.minDistance = 5f; sfx.maxDistance = 90f; sfx.volume = 1f;
        musicSource = NewSource("Bridge Chase Music", musicGroup, 0f, true);

        noticeCam = NewCam("Look at the Runner (from Khalid)", 50f);
        followCam = NewCam("The plank (from the ravine's edge)", followFov);
    }

    AudioSource NewSource(string n, AudioMixerGroup group, float spatial, bool loop)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false; s.loop = loop; s.spatialBlend = spatial; s.dopplerLevel = 0f; s.volume = 0f;
        s.outputAudioMixerGroup = group;
        return s;
    }

    CinemachineVirtualCamera NewCam(string n, float fov)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var c = go.AddComponent<CinemachineVirtualCamera>();
        c.m_Lens.FieldOfView = fov; c.m_Lens.NearClipPlane = 0.08f;
        c.Priority = 0;
        c.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
        return c;
    }

    void Start()
    {
        if (runner != null) runner.gameObject.SetActive(false);
        if (adel != null) adel.gameObject.SetActive(false);
        UpdateIdleState();
    }

    bool KnifePicked => SaveManager.instance != null && !string.IsNullOrEmpty(knifeHandleId) && SaveManager.instance.IsItemPickedUp(knifeHandleId);
    bool BridgeRepaired => SaveManager.instance != null && !string.IsNullOrEmpty(bridgeRepairId) && SaveManager.instance.IsInteractableUsed(bridgeRepairId);
    public static bool AlreadyDone => StoryFlags.Has(DoneFlag);
    bool Active => phase == Phase.Intro || phase == Phase.Chase || phase == Phase.Ending;

    /// <summary>Not playing: the plank where the story has it (a checkpoint / save restore can change that at any time).</summary>
    void UpdateIdleState()
    {
        if (debugArmed) { phase = Phase.Waiting; SetPlank(PlankState.OnBridge); return; }
        if (AlreadyDone) { SetPlank(PlankState.Fallen); phase = Phase.Done; return; }
        phase = Phase.Waiting;
        SetPlank(KnifePicked && !BridgeRepaired ? PlankState.OnBridge : PlankState.Hidden);
    }

    void SetPlank(PlankState s)
    {
        if (plank == null || s == plankState) return;
        plankState = s;
        plankT = -1f;
        plank.gameObject.SetActive(s != PlankState.Hidden);
        if (s == PlankState.OnBridge) plank.SetPositionAndRotation(plankP0, plankR0);
        if (s == PlankState.Fallen) plank.SetPositionAndRotation(fallenPos, FallenRot);
        // walkable only while it bridges the gap (the Runner's feet find it); down there it's just scenery
        if (plankColliders != null) foreach (var c in plankColliders) if (c != null) c.enabled = s == PlankState.OnBridge;
    }

    Quaternion FallenRot => Quaternion.AngleAxis(fallenYaw, Vector3.up) * Quaternion.AngleAxis(-fallenTilt, Vector3.forward) * plankR0;

    // ------------------------------------------------------------------ trigger

    bool PlayerInArea(Player p) => new Bounds(areaCenter, areaSize).Contains(p.transform.position);

    bool CanStart(Player p)
    {
        if (GameManager.IsPaused || CheckpointManager.IsLoadingCheckpoint) return false;
        if (KidnapSequence.IsPlaying || KidnapSequence.ChaseActive) return false;
        if (p.pauseMovement) return false;                  // something else has him (a pickup walk, a grab...)
        if (ItemInspectionHandler.instance != null && ItemInspectionHandler.instance.InspectionMenuIsActive()) return false;
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) return false;
        return true;
    }

    void Update()
    {
        if (restoreBlendAt >= 0f && Time.time >= restoreBlendAt) { if (brain != null && blendSaved) brain.m_DefaultBlend = savedBlend; restoreBlendAt = -1f; }
        if (Active)
        {
            if (skipping) return;
            float dt = Time.deltaTime;
            t += dt; beatT += dt;
            if (phase == Phase.Intro) UpdateIntro(dt);
            else if (phase == Phase.Chase) UpdateChase(dt);
            else UpdateEnding(dt);
            UpdatePlank(dt);
            return;
        }
        UpdateIdleState();
        UpdatePlank(Time.deltaTime);
        var p = Player.instance;
        if (phase == Phase.Waiting && plankState == PlankState.OnBridge && p != null && PlayerInArea(p) && CanStart(p)) Begin(false);
        // safety net: he got north of the main road without walking into the area (shouldn't happen with the Block 1 gate
        // buried and the tree lines) - it's quietly over, so the plank can't be left lying across the ravine
        else if (phase == Phase.Waiting && plankState == PlankState.OnBridge && p != null && p.transform.position.x > northOfRoadX
                 && !PlayerInArea(p) && !CheckpointManager.IsLoadingCheckpoint)
            StoryFlags.Set(DoneFlag);
    }

    // ------------------------------------------------------------------ 1. the mini cutscene

    void Begin(bool debugForce)
    {
        var player = Player.instance;
        if (player == null || Active) return;
        if (AlreadyDone && !debugForce && !debugArmed) return;
        debugArmed = false;
        phase = Phase.Intro; IsPlaying = true;
        t = 0f; beatT = 0f; skipHeld = 0f; skipping = returning = false; dropPlayed = false;
        hintUntil = -1f; lastHint = -999f;
        StoryFlags.Set(DoneFlag);
        SetPlank(PlankState.OnBridge);

        HoldKhalid(player, true);

        // the Runner, crouched behind the parked car nearest to where he came in
        Vector3 k = player.transform.position;
        spot = null;
        foreach (var s in spawns) if (spot == null || Flat(s.hide - k).sqrMagnitude < Flat(spot.hide - k).sqrMagnitude) spot = s;
        BuildPath(spot);
        crouchNow = spot != null ? spot.crouch : 0f;
        if (runner != null)
        {
            runner.Appear(pts[0] + Vector3.down * crouchNow, k);
            runner.Idle();
            runnerS = 0f; runnerY = pts[0].y;
        }
        beat = Beat.Hidden;

        if (adel != null)
        {
            adel.gameObject.SetActive(true);
            adel.Play("Idle", 0, 0f); adel.speed = 1f; adel.Update(0f);
            if (adelHips != null) adelHipHeight = Mathf.Max(0.3f, adelHips.position.y - adel.transform.position.y);
            adel.speed = 0.85f; adel.Play("Run", 0, 0f);   // kicking on the shoulder
            PoseAdelCarried();
        }
        if (steps == null) steps = gameObject.AddComponent<ScriptedFootsteps>();
        steps.End();

        // camera: ease over Khalid's shoulder onto the car it comes out from behind, lens closing
        var cam = Camera.main;
        brain = cam != null ? cam.GetComponent<CinemachineBrain>() : null;
        if (brain != null)
        {
            if (!blendSaved || restoreBlendAt < 0f) { savedBlend = brain.m_DefaultBlend; blendSaved = true; }
            restoreBlendAt = -1f;
            brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.8f);
        }
        StartNoticeCam(player);
        if (!musicSourceBusy) StartMusic();
    }

    bool musicSourceBusy => musicSource != null && musicSource.isPlaying;

    void UpdateIntro(float dt)
    {
        if (runner == null) { BeginChase(); return; }
        if (beat == Beat.Hidden)
        {
            var player = Player.instance;
            if (player != null) runner.FaceDirection(player.transform.position - runner.transform.position, dt);
            runner.transform.position = pts[0] + Vector3.down * crouchNow;
            if (t >= RunAfter) StartRunning();
        }
        else
        {
            runnerS = Mathf.Min(shoveS, runnerS + introRunSpeed * dt);
            PlaceRunner(introRunSpeed, dt);
        }
        if (t >= introSeconds) BeginChase();
    }

    /// <summary>Up from behind the car and away (its footsteps start here: tracking them crouched would miss every step).</summary>
    void StartRunning()
    {
        crouchNow = 0f; runnerY = pts[0].y;
        runner.transform.position = pts[0];
        runner.Run();
        beat = Beat.Running; beatT = 0f;
        var ra = RunnerAnimator;
        if (ra != null && steps != null) steps.Track(ra, footstepVolume);
    }

    // ------------------------------------------------------------------ 2. the chase

    void BeginChase()
    {
        var player = Player.instance;
        if (beat == Beat.Hidden && runner != null) StartRunning();
        phase = Phase.Chase;
        // the view eases back to Khalid (the shoulder camera is right by him, nothing to fly through)
        if (noticeCam != null) noticeCam.Priority = 0;
        RestoreBlendSoon(1.0f);
        ReleaseKhalid(player);
        turnKhalid = false;
        // the storm thickens once the chase is on (in the cutscene he has to be seen coming out from behind the car)
        if (EnviroSkyLite.instance != null && fogNow < 0f) { fogBase = EnviroSkyLite.instance.fogSettings.heightDensity; fogNow = fogBase; }
        fogGoal = chaseFogDensity;
        if (player == null) return;
        var pm = player.GetComponent<PlayerMovement>();
        if (pm != null && !sprintChanged)
        {
            savedSprint = pm.SprintSpeed; savedStamina = pm.useStaminaForSprint; sprintChanged = true;
            pm.SprintSpeed = chaseSprintSpeed; pm.useStaminaForSprint = false;
        }
        // no pipe, no gun: he just runs
        PlayerMeleeWeapon.SetCutscene(PlayerMeleeWeapon.CutsceneHold.Away);
        HideGun(player, true);
        playerS = ProjectOnPath(player.transform.position, 0f);
        lastPlayerPos = player.transform.position; waitingTime = 0f; farTime = 0f;
        lastPlayerS = playerS; chaseEffort = 3f;     // he starts out chasing
    }

    void UpdateChase(float dt)
    {
        var player = Player.instance;
        if (player == null) return;
        if (player.playerWeaponSystem != null && player.playerWeaponSystem.weaponIsEnabled) HideGun(player, false);

        playerS = ProjectOnPath(player.transform.position, playerS);
        AdvanceRunner(dt);
        UpdateFallingBehind(player, dt);

        // over the plank and waiting on the far side: once he's close it goes in
        if (beat == Beat.AtPlank)
        {
            runner.FaceDirection(player.transform.position - runner.transform.position, dt);
            if (hintUntil < 0f && Flat(runner.transform.position - player.transform.position).magnitude <= shoveWhenWithin) BeginEnding();
        }
    }

    /// <summary>~gapWanted ahead of Khalid along the line; waits in the fog if he falls far behind; flat out over the plank.</summary>
    void AdvanceRunner(float dt)
    {
        if (runner == null || beat == Beat.AtPlank) return;
        float gap = runnerS - playerS;
        float straight = Player.instance != null ? Flat(runner.transform.position - Player.instance.transform.position).magnitude : 0f;
        bool dash = runner.transform.position.x > dashFromX;
        float sp = dash ? runnerMaxSpeed : Mathf.Clamp(chaseSprintSpeed + (gapWanted - gap) * 0.5f, runnerMinSpeed, runnerMaxSpeed);
        // it only ever stands still where the camera can't see it (user: "stops when is not visible, kinda like some sort of
        // developer secret"): swallowed by the fog, or off screen and well ahead - there it waits for Khalid to catch up. On screen
        // and in range it runs (a reminder changes nothing: out of sight it stays put, in sight it keeps going)
        bool waiting = beat == Beat.Waiting;
        bool wait = !dash && RunnerUnseen(straight, waiting);
        if (wait != waiting) { beat = wait ? Beat.Waiting : Beat.Running; beatT = 0f; if (wait) runner.Idle(); else runner.Run(); }
        if (wait) sp = 0f;
        runnerS = Mathf.Min(shoveS, runnerS + sp * dt);
        PlaceRunner(wait ? runnerMinSpeed : sp, dt);
        // standing there with Adel on its shoulder, it turns to look back at him
        if (wait && Player.instance != null) runner.FaceDirection(Player.instance.transform.position - runner.transform.position, dt);
        if (runnerS >= shoveS - 0.001f) { runner.Idle(); beat = Beat.AtPlank; beatT = 0f; }
    }

    /// <summary>Can the camera see it? No: lost in the storm (further than sightDistance from the camera), or out of the frame
    /// while it's well ahead of Khalid.</summary>
    bool RunnerUnseen(float straight, bool waiting)
    {
        var cam = Camera.main;
        Vector3 chest = RunnerChest;
        float camDist = cam != null ? Vector3.Distance(cam.transform.position, chest) : straight;
        if (camDist > (waiting ? sightDistance - 2f : sightDistance)) return true;
        if (cam == null) return false;
        Vector3 v = cam.WorldToViewportPoint(chest);
        bool onScreen = v.z > 0f && v.x > -0.15f && v.x < 1.15f && v.y > -0.15f && v.y < 1.15f;
        return !onScreen && straight > offScreenRestDistance;
    }

    /// <summary>The park chase's reminder: Khalid hangs back while it waits, or wanders off -> the view swings to it and he
    /// says "I better chase him!". Wandering off he also stops and turns to face it.</summary>
    void UpdateFallingBehind(Player player, float dt)
    {
        Vector3 pos = player.transform.position;
        Vector3 vel = dt > 0f ? Flat(pos - lastPlayerPos) / dt : Vector3.zero;
        float speed = vel.magnitude;
        lastPlayerPos = pos;
        float dist = runner != null ? Flat(runner.transform.position - pos).magnitude : 0f;
        // is he chasing? running its way (toward it, or on along its line). Then a reminder would only ruin the moment (user)
        float toward = runner != null && dist > 0.1f ? Vector3.Dot(vel, Flat(runner.transform.position - pos) / dist) : 0f;
        float along = dt > 0f ? (playerS - lastPlayerS) / dt : 0f;
        lastPlayerS = playerS;
        chaseEffort = Mathf.Lerp(chaseEffort, Mathf.Max(toward, along), 1f - Mathf.Exp(-3f * dt));
        // (however far behind he is: running its way is doing it right - user)
        bool chasing = chaseEffort > 0.8f;
        bool runnerStill = beat == Beat.Waiting || beat == Beat.AtPlank;
        if (runnerStill && speed < 1.5f) waitingTime += dt; else waitingTime = 0f;
        if (dist > farDistance && !chasing) farTime += dt; else farTime = 0f;
        bool canHint = hintUntil < 0f && runner != null && runner.gameObject.activeSelf;
        bool hangingBack = waitingTime >= hintAfterWaiting && Time.time - lastHint >= hintCooldown;
        bool tooFar = farTime >= farSeconds && Time.time - lastHint >= (dist > leashDistance ? hintSeconds + 1f : farCooldown);
        if (canHint && (hangingBack || tooFar))
        {
            lastHint = Time.time; hintUntil = Time.time + hintSeconds; waitingTime = 0f; farTime = 0f;
            turnKhalid = tooFar;
            if (tooFar && !player.pauseMovement) { player.pauseMovement = true; pausedByHint = true; }
            if (brain != null)
            {
                if (restoreBlendAt < 0f) { savedBlend = brain.m_DefaultBlend; blendSaved = true; }
                restoreBlendAt = -1f;
                brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.7f);
            }
            // the reminder is the game camera itself (user: "make it like the original camera exactly"): same spot behind
            // Khalid, same lens, no zoom - it only turns toward where the Runner is
            var cam = Camera.main;
            StartNoticeCam(player);
            noticeZoom = false;
            if (cam != null) noticeOffset = cam.transform.position - player.transform.position;
            UpdateNoticeCam(true);
            if (Time.time - lastLine >= lineCooldown) { lastLine = Time.time; Say(hintLine, 2.2f); }
        }
        if (hintUntil >= 0f && Time.time >= hintUntil) EndHint();
    }

    void EndHint()
    {
        if (pausedByHint && Player.instance != null) Player.instance.pauseMovement = false;
        pausedByHint = false;
        if (hintUntil >= 0f)
        {
            turnKhalid = false;
            if (noticeCam != null) noticeCam.Priority = 0;
            RestoreBlendSoon(0.8f);
        }
        hintUntil = -1f;
    }

    // ------------------------------------------------------------------ 3. the plank goes in

    void BeginEnding()
    {
        var player = Player.instance;
        EndHint();
        phase = Phase.Ending;
        HoldKhalid(player, false);
        turnKhalid = true;
        beat = Beat.Turning; beatT = 0f;
        // the view eases over to the near edge of the ravine to watch it
        if (brain != null)
        {
            if (restoreBlendAt < 0f) { savedBlend = brain.m_DefaultBlend; blendSaved = true; }
            restoreBlendAt = -1f;
            brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 1.0f);
        }
        followVel = Vector3.zero;
        UpdateFollowCam(true);
        followCam.Priority = 80;
        if (fogNow >= 0f) fogGoal = endingFogDensity;
    }

    void UpdateEnding(float dt)
    {
        if (runner == null || beat == Beat.Gone)
        {
            if (!returning && beatT >= BackAfterLeaving) { returning = true; StartCoroutine(Co_EaseBack()); }
            return;
        }
        switch (beat)
        {
            case Beat.Turning:
                // round to face the plank (back the way it came)
                runner.FaceDirection(-PathDirAtShove(), dt * 2.2f);
                if (beatT >= TurnTime)
                {
                    var ra = RunnerAnimator;
                    if (ra != null) { ra.speed = 1.3f; ra.CrossFadeInFixedTime("Attack", 0.12f, 0, 0.25f); }
                    beat = Beat.Shoving; beatT = 0f;
                }
                break;
            case Beat.Shoving:
                runner.FaceDirection(-PathDirAtShove(), dt);
                if (plankT < 0f && beatT >= ShoveHit) StartPlankFall();
                if (beatT >= ShoveEnd)
                {
                    var ra = RunnerAnimator;
                    if (ra != null) ra.CrossFadeInFixedTime("Movement", 0.2f);
                    runner.Idle();
                    beat = Beat.Staring; beatT = 0f;
                }
                break;
            case Beat.Staring:
                // it looks back across at Khalid's side, Adel over its shoulder
                runner.FaceDirection(-PathDirAtShove(), dt);
                if (beatT >= StareEnd) { runner.Run(); beat = Beat.Leaving; beatT = 0f; }
                break;
            case Beat.Leaving:
                runnerS = Mathf.Min(PathLength, runnerS + escapeSpeed * dt);
                PlaceRunner(escapeSpeed, dt);
                if (runnerS >= PathLength - 0.05f)
                {
                    runner.Vanish();
                    if (adel != null) adel.gameObject.SetActive(false);
                    if (steps != null) steps.End();
                    beat = Beat.Gone; beatT = 0f;
                }
                break;
        }
        if (!returning) HandleSkip();
    }

    /// <summary>The view eases back from the ravine's edge to Khalid (open road between, short way), then he's free.</summary>
    IEnumerator Co_EaseBack()
    {
        if (followCam != null) followCam.Priority = 0;
        yield return new WaitForSeconds(1.0f);
        Finish();
    }

    /// <summary>Skip (hold E in the ending): dip to black, everything where it ends up, back in.</summary>
    IEnumerator Co_Skip()
    {
        returning = true; skipping = true;
        if (FadeScreenUI.instance != null) FadeScreenUI.instance.FadeOut(0.35f);
        yield return new WaitForSecondsRealtime(0.4f);
        if (brain != null) brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);
        Finish();
        if (FadeScreenUI.instance != null) FadeScreenUI.instance.FadeIn(backFadeIn);
    }

    void Finish()
    {
        var player = Player.instance;
        SetPlank(PlankState.Fallen);
        if (runner != null) runner.Vanish();
        if (adel != null) adel.gameObject.SetActive(false);
        if (steps != null) steps.End();
        beat = Beat.Gone;
        EndHint();
        if (noticeCam != null) noticeCam.Priority = 0;
        if (followCam != null) followCam.Priority = 0;
        RestoreBlendSoon(skipping ? 0.05f : 1.1f);
        ReleaseKhalid(player);
        EndChaseState(player, false);
        if (CutsceneSkipper.Instance != null) CutsceneSkipper.Instance.HideHint();
        StopMusic();
        fogGoal = -1f;          // the storm settles back to the weather's own
        phase = Phase.Done; IsPlaying = false; skipping = false;
        StartCoroutine(Co_After());
    }

    IEnumerator Co_After()
    {
        // he's himself again: dying later, Continue or a reload come back here with the bridge gone, never before it
        yield return null; yield return null; yield return null;
        if (CheckpointManager.instance != null) CheckpointManager.instance.TriggerCheckpoint(CheckpointName);
    }

    // ------------------------------------------------------------------ khalid

    void HoldKhalid(Player player, bool firstTime)
    {
        if (player == null) return;
        if (!player.pauseMovement) { player.pauseMovement = true; pausedMovement = true; }
        var pm = player.GetComponent<PlayerMovement>();
        if (pm != null) pm.SetExternalMoveInput(Vector2.zero);
        if (player.playerWeaponSystem != null) player.playerWeaponSystem.ExitOutOfAiming();
        Disable(player.playerWeaponSystem);
        Disable(player.GetComponent<PlayerRoll>());
        Disable(player.GetComponent<PlayerMeleeAttack>());
        Disable(player.GetComponent<DoorInteractionHandler>());
        if (player.health != null && !player.health.Invincible) { player.health.Invincible = true; madeInvincible = true; }
        if (player.animator != null)
        {
            player.animator.SetBool("Turning", false);
            player.animator.SetFloat("x", 0f); player.animator.SetFloat("y", 0f); player.animator.SetFloat("Velocity", 0f);
        }
        turnKhalid = true;
    }

    void Disable(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false; disabledBehaviours.Add(b);
    }

    void ReleaseKhalid(Player player)
    {
        if (player != null)
        {
            if (pausedMovement) player.pauseMovement = false;
            if (madeInvincible && player.health != null) player.health.Invincible = false;
        }
        pausedMovement = false; madeInvincible = false;
        foreach (var b in disabledBehaviours) if (b != null) b.enabled = true;
        disabledBehaviours.Clear();
    }

    /// <summary>Chase speed, pipe and gun back to normal.</summary>
    void EndChaseState(Player player, bool disabling)
    {
        if (player != null && sprintChanged)
        {
            var pm = player.GetComponent<PlayerMovement>();
            if (pm != null) { pm.SprintSpeed = savedSprint; pm.useStaminaForSprint = savedStamina; }
        }
        bool hadChase = sprintChanged;
        sprintChanged = false;
        if (!hadChase) return;
        PlayerMeleeWeapon.SetCutscene(PlayerMeleeWeapon.CutsceneHold.None);
        if (player == null) return;
        if (disabling)
        {
            if (player.playerWeaponSystem != null)
                player.playerWeaponSystem.weaponIsEnabled = savedWeapon || (player.inventory != null && player.inventory.HasWeapon());
            return;
        }
        StartCoroutine(Co_GunBack());
    }

    IEnumerator Co_GunBack()
    {
        var player = Player.instance;
        if (player == null) yield break;
        if (player.playerWeaponSystem != null) player.playerWeaponSystem.weaponIsEnabled = savedWeapon || (player.inventory != null && player.inventory.HasWeapon());
        // the pipe decides first (it hides the gun when it comes out)
        yield return null; yield return null;
        if (gunEnabler != null && gunEnabler.WeaponObject != null && player.playerWeaponSystem != null && player.playerWeaponSystem.weaponIsEnabled
            && !PlayerMeleeWeapon.Equipped && !player.playerWeaponSystem.isAiming)
            gunEnabler.WeaponObject.SetActive(true);
        savedWeapon = false;
    }

    void HideGun(Player player, bool remember)
    {
        var ws = player.playerWeaponSystem;
        if (ws == null) return;
        if (remember) savedWeapon = ws.weaponIsEnabled || savedWeapon;
        ws.ExitOutOfAiming();
        ws.weaponIsEnabled = false;
        if (gunEnabler == null) gunEnabler = player.GetComponent<PlayerWeaponEnabler>();
        if (gunEnabler != null && gunEnabler.WeaponObject != null) gunEnabler.WeaponObject.SetActive(false);
    }

    void RestoreBlendSoon(float after)
    {
        if (brain != null && blendSaved) restoreBlendAt = Time.time + after;
    }

    // ------------------------------------------------------------------ runner

    Animator RunnerAnimator => runner != null ? runner.GetComponentInChildren<Animator>() : null;

    void PlaceRunner(float speed, float dt)
    {
        Vector3 p = PathPoint(runnerS, out Vector3 dir);
        float gy = Ground(p).y;
        runnerY = dt > 0f ? Mathf.Lerp(runnerY, gy, 1f - Mathf.Exp(-14f * dt)) : gy;
        p.y = runnerY;
        runner.transform.position = p;
        if (dt > 0f) runner.FaceDirection(dir, dt * 1.6f); else runner.FaceInstant(dir);
        var ra = RunnerAnimator;
        if (ra != null && !ra.GetCurrentAnimatorStateInfo(0).IsName("Attack")) ra.speed = Mathf.Clamp(speed / runnerClipSpeed, 0.9f, 1.75f);
    }

    // ---- the plank: slides toward the near side, its far end drops off the edge, down it goes and wedges in the crack
    void StartPlankFall()
    {
        if (plank == null) return;
        SetPlank(PlankState.OnBridge);
        plankState = PlankState.Falling;
        plankT = 0f; dropPlayed = false;
        if (plankColliders != null) foreach (var c in plankColliders) if (c != null) c.enabled = false;
        PlayAt(plankCreak, plank.position, 1f);
    }

    void UpdatePlank(float dt)
    {
        if (plank == null || plankState != PlankState.Falling || plankT < 0f) return;
        plankT += dt;
        Vector3 toward = -PathDirAtShove();                      // from the far end back toward the near side
        Vector3 slid = plankP0 + toward * slideDistance;
        // the far end dips as it comes off its edge
        Quaternion slidRot = Quaternion.AngleAxis(-8f, Vector3.Cross(Vector3.up, toward)) * plankR0;
        float fallEnd = slideSeconds + fallSeconds;
        if (plankT < slideSeconds)
        {
            float u = plankT / slideSeconds;
            plank.SetPositionAndRotation(Vector3.Lerp(plankP0, slid, Mathf.SmoothStep(0f, 1f, u)), Quaternion.Slerp(plankR0, slidRot, u * u));
        }
        else if (plankT < fallEnd)
        {
            float u = (plankT - slideSeconds) / fallSeconds;
            // gravity: slow then fast; the far end dives first (rotation leads the drop)
            plank.SetPositionAndRotation(Vector3.Lerp(slid, fallenPos, u * u), Quaternion.Slerp(slidRot, FallenRot, Mathf.Sin(u * Mathf.PI * 0.5f)));
        }
        else
        {
            if (!dropPlayed) { dropPlayed = true; PlayAt(plankDrop, fallenPos, 1f); }
            float v = Mathf.Clamp01((plankT - fallEnd) / 0.3f);
            plank.SetPositionAndRotation(fallenPos + Vector3.up * (0.14f * Mathf.Sin(Mathf.PI * v) * (1f - v)), FallenRot);
            if (v >= 1f) { plankState = PlankState.Fallen; plankT = -1f; }
        }
    }

    Vector3 PathDirAtShove()
    {
        // which way it was running onto the shove spot (the plank lies along that line)
        Vector3 d = PathDir(shoveS - 0.5f);
        return d.sqrMagnitude > 0.0001f ? d : Vector3.right;
    }

    void LateUpdate()
    {
        UpdateFog(Time.deltaTime);
        if (!Active || skipping) return;
        if (noticeCam != null && noticeCam.Priority > 0) UpdateNoticeCam(false);
        if (followCam != null && followCam.Priority > 0) UpdateFollowCam(false);
        PoseAdelCarried();
        // Khalid looks where it is (in the cutscene parts, and when a reminder stops him)
        var player = Player.instance;
        if ((turnKhalid || phase != Phase.Chase) && player != null && runner != null && runner.gameObject.activeSelf)
        {
            // (a reminder turns him the way its camera looks: at it, or along its line where a wall hides it)
            Vector3 look = phase == Phase.Chase && hintUntil >= 0f ? hintLook : runner.transform.position;
            Vector3 d = Flat(look - player.transform.position);
            if (d.sqrMagnitude > 0.01f)
                player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, Quaternion.LookRotation(d), 220f * Time.deltaTime);
        }
    }

    /// <summary>Enviro eases the fog back to its weather preset every frame: ours goes on top (LateUpdate) while it lasts.</summary>
    void UpdateFog(float dt)
    {
        if (fogNow < 0f || EnviroSkyLite.instance == null) return;
        float goal = fogGoal >= 0f ? fogGoal : fogBase;
        float span = Mathf.Max(0.05f, Mathf.Abs(chaseFogDensity - fogBase));
        float rate = span / Mathf.Max(0.05f, goal > fogNow ? fogRampIn : fogRampOut);
        fogNow = Mathf.MoveTowards(fogNow, goal, rate * dt);
        EnviroSkyLite.instance.fogSettings.heightDensity = fogNow;
        if (fogGoal < 0f && Mathf.Approximately(fogNow, fogBase)) fogNow = -1f;     // back to normal: Enviro's again
    }

    // ------------------------------------------------------------------ cameras

    // where its chest is once standing (while it's still crouched the camera already looks at where it comes up)
    Vector3 RunnerChest => runner.transform.position + Vector3.up * (1.3f + crouchNow);

    bool noticeZoom = true;
    // the game camera's own lens (ThirdPersonVCam: 60 deg, near 0.2, FAR 25 - the short far clip is part of what hides the
    // distance in the storm; a new vcam's default far clip of 5000 showed everything)
    float gameFov = 60f, gameNear = 0.2f, gameFar = 25f;

    /// <summary>Read the gameplay camera's lens while it is the one live (no blend, none of ours on).</summary>
    void CaptureGameLens()
    {
        var cam = Camera.main;
        if (cam == null || (brain != null && brain.IsBlending)) return;
        if ((noticeCam != null && noticeCam.Priority > 0) || (followCam != null && followCam.Priority > 0)) return;
        gameFov = cam.fieldOfView; gameNear = cam.nearClipPlane; gameFar = cam.farClipPlane;
    }

    void ApplyGameClip(CinemachineVirtualCamera c)
    {
        if (c == null) return;
        c.m_Lens.NearClipPlane = gameNear; c.m_Lens.FarClipPlane = gameFar;
    }

    void StartNoticeCam(Player player)
    {
        CaptureGameLens();
        ApplyGameClip(noticeCam); ApplyGameClip(followCam);
        noticeZoom = true;
        noticeOffset = NoticeCameraOffset(player);
        noticeStart = Time.time;
        UpdateNoticeCam(true);
        noticeCam.Priority = 75;
    }

    /// <summary>From beside Khalid onto the Runner, the lens closing to ~5 m of frame at its distance.</summary>
    void UpdateNoticeCam(bool snap)
    {
        var player = Player.instance;
        if (noticeCam == null || player == null || runner == null) return;
        Vector3 pos = player.transform.position + noticeOffset;
        Vector3 target = noticeZoom ? RunnerChest : (hintLook = HintLookTarget(pos));
        Quaternion rot = Quaternion.LookRotation(target - pos, Vector3.up);
        noticeCam.transform.SetPositionAndRotation(pos, snap ? rot : Quaternion.Slerp(noticeCam.transform.rotation, rot, 1f - Mathf.Exp(-8f * Time.deltaTime)));
        float dist = Vector3.Distance(pos, target);
        float tight = Mathf.Clamp(2f * Mathf.Atan(2.6f / Mathf.Max(1f, dist)) * Mathf.Rad2Deg, 9f, 40f);
        float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(noticeStart + 0.2f, noticeStart + 1.2f, Time.time));
        noticeCam.m_Lens.FieldOfView = noticeZoom ? Mathf.Lerp(50f, tight, u) : gameFov;
    }

    Vector3 hintLook;

    /// <summary>Where a reminder looks: at the Runner if nothing is in the way, else the furthest point of its line the camera
    /// can see (never a wall filling the screen).</summary>
    Vector3 HintLookTarget(Vector3 camPos)
    {
        Vector3 chest = RunnerChest;
        if (!Blocked(camPos, chest)) return chest;
        for (float s = runnerS; s >= playerS - 4f; s -= 2f)
        {
            Vector3 p = PathPoint(Mathf.Max(0f, s), out _) + Vector3.up * 1.3f;
            if (!Blocked(camPos, p)) return p;
        }
        var player = Player.instance;
        return player != null ? player.transform.position + player.transform.forward * 10f + Vector3.up * 1.3f : chest;
    }

    /// <summary>The ending: behind the Runner's line at the near edge of the ravine, watching it shove the plank in and go.</summary>
    void UpdateFollowCam(bool snap)
    {
        if (followCam == null || runner == null || pts.Count < 2) return;
        Vector3 dir = PathDirAtShove();
        Vector3 at = PathPoint(shoveS, out _);
        Vector3 want = at - dir * followBack;
        if (want.x > cameraMaxX) want.x = cameraMaxX;
        want = Ground(new Vector3(want.x, at.y, want.z)) + Vector3.up * followUp;
        Vector3 chest = RunnerChest;
        for (float lift = 0f; lift <= 4.5f; lift += 1.5f)
            if (!Blocked(want + Vector3.up * lift, chest)) { want += Vector3.up * lift; break; }
        Vector3 pos = snap ? want : Vector3.SmoothDamp(followCam.transform.position, want, ref followVel, 0.45f);
        Vector3 look = chest;
        if (plank != null && (plankState == PlankState.Falling || (plankState == PlankState.Fallen && beat == Beat.Staring && beatT < 0.8f)))
            look = Vector3.Lerp(look, plank.position, 0.4f);
        Quaternion rot = Quaternion.LookRotation(look - pos, Vector3.up);
        followCam.transform.SetPositionAndRotation(pos, snap ? rot : Quaternion.Slerp(followCam.transform.rotation, rot, 1f - Mathf.Exp(-6f * Time.deltaTime)));
        followCam.m_Lens.FieldOfView = followFov;
    }

    static bool Blocked(Vector3 a, Vector3 b, Transform ignore = null)
    {
        Vector3 d = b - a;
        foreach (var h in Physics.RaycastAll(a, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (Player.instance != null && h.collider.transform.IsChildOf(Player.instance.transform)) continue;
            if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
            if (IsInvisible(h.collider)) continue;
            return true;
        }
        return false;
    }

    static bool IsInvisible(Collider c)
    {
        // invisible blockers (Boundaries cubes, the tree-line walls) have no renderer or it's switched off
        var r = c.GetComponent<Renderer>();
        return r != null && !r.enabled;
    }

    /// <summary>Over Khalid's shoulder if that sees the Runner, otherwise lifted until it clears what's in the way.</summary>
    Vector3 NoticeCameraOffset(Player player)
    {
        Vector3 k = player.transform.position;
        Vector3 head = k + Vector3.up * 1.7f;
        Vector3 target = runner != null ? RunnerChest : head + player.transform.forward * 10f;
        Vector3 to = Flat(target - head);
        if (to.sqrMagnitude < 0.01f) to = player.transform.forward;
        to.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, to);
        // well off to the side: his head stays out of the frame once the lens closes in. The car it hides behind doesn't
        // count (the camera stays low and sees it come out round it).
        Transform cover = phase == Phase.Intro && spot != null ? spot.cover : null;
        foreach (float lift in new[] { 0.45f, 1.4f, 2.5f, 4f, 6f })
        {
            Vector3 p = head - to * 1.8f + right * 1.0f + Vector3.up * lift;
            if (!Blocked(head, p) && !Blocked(p, target, cover)) return p - k;
        }
        return head - to * 0.6f + Vector3.up * 1.5f - k;
    }

    // ------------------------------------------------------------------ adel

    /// <summary>Adel over the Runner's shoulder (fireman's carry), as in the kidnap.</summary>
    void PoseAdelCarried()
    {
        if (adel == null || runner == null || !adel.gameObject.activeSelf) return;
        Vector3 f = Flat(runner.transform.forward).normalized;
        Vector3 shoulder = runnerShoulder != null ? runnerShoulder.position : runner.transform.position + Vector3.up * 1.55f;
        Vector3 up = (-f + Vector3.down * 0.35f).normalized;
        Vector3 fwd = Vector3.ProjectOnPlane(Vector3.down, up).normalized;
        adel.transform.SetPositionAndRotation(shoulder + Vector3.up * 0.12f + f * 0.05f - up * adelHipHeight, Quaternion.LookRotation(fwd, up));
    }

    // ------------------------------------------------------------------ path

    void BuildPath(CoverSpot from)
    {
        pts.Clear(); cum.Clear();
        pts.Add(Ground(from != null ? from.hide : transform.position));
        // out from behind its car and along the main road to the side road
        if (from != null) foreach (var p in from.leadIn) pts.Add(Ground(p));
        foreach (var p in route) pts.Add(Ground(p));
        int shoveIndex = pts.Count - 1;
        foreach (var p in away) pts.Add(Ground(p));
        cum.Add(0f);
        for (int i = 1; i < pts.Count; i++) cum.Add(cum[i - 1] + Flat(pts[i] - pts[i - 1]).magnitude);
        shoveS = cum[shoveIndex];
    }

    float PathLength => cum.Count > 0 ? cum[cum.Count - 1] : 0f;

    Vector3 PathPoint(float s, out Vector3 dir)
    {
        dir = Vector3.forward;
        if (pts.Count == 0) return Vector3.zero;
        if (pts.Count == 1) return pts[0];
        int i = 1;
        while (i < pts.Count - 1 && cum[i] < s) i++;
        float segLen = Mathf.Max(0.0001f, cum[i] - cum[i - 1]);
        float u = Mathf.Clamp01((s - cum[i - 1]) / segLen);
        dir = Flat(pts[i] - pts[i - 1]).normalized;
        if (i < pts.Count - 1)
        {
            float toEnd = cum[i] - s;
            if (toEnd < 1.5f) dir = Vector3.Slerp(dir, Flat(pts[i + 1] - pts[i]).normalized, 0.5f * (1f - toEnd / 1.5f)).normalized;
        }
        return Vector3.Lerp(pts[i - 1], pts[i], u);
    }

    Vector3 PathDir(float s) { PathPoint(Mathf.Max(0f, s), out Vector3 d); return d; }

    /// <summary>Arc length of the point on the line (up to the plank) closest to 'pos', searched around the last answer.</summary>
    float ProjectOnPath(Vector3 pos, float near)
    {
        if (pts.Count < 2) return 0f;
        float best = near, bestD = float.MaxValue;
        for (int i = 1; i < pts.Count; i++)
        {
            if (cum[i - 1] >= shoveS) break;
            if (cum[i] < near - 25f || cum[i - 1] > near + 40f) continue;
            Vector3 a = Flat(pts[i - 1]), b = Flat(pts[i]), q = Flat(pos);
            Vector3 ab = b - a;
            float u = Mathf.Clamp01(Vector3.Dot(q - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            float d = (a + ab * u - q).sqrMagnitude;
            if (d < bestD) { bestD = d; best = cum[i - 1] + (cum[i] - cum[i - 1]) * u; }
        }
        return best;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    /// <summary>Highest solid ground under p (the plank counts; Khalid, triggers and invisible blockers don't).</summary>
    Vector3 Ground(Vector3 p)
    {
        var hits = Physics.RaycastAll(new Vector3(p.x, p.y + 2.5f, p.z), Vector3.down, 9f, ~0, QueryTriggerInteraction.Ignore);
        float bestY = float.NegativeInfinity;
        foreach (var h in hits)
        {
            if (Player.instance != null && h.collider.transform.IsChildOf(Player.instance.transform)) continue;
            bool isPlank = plank != null && h.collider.transform.IsChildOf(plank);
            if (!isPlank && IsInvisible(h.collider)) continue;
            if (h.point.y > bestY) bestY = h.point.y;
        }
        if (bestY > float.NegativeInfinity) p.y = bestY;
        return p;
    }

    // ------------------------------------------------------------------ sound / lines

    void PlayAt(AudioClip clip, Vector3 pos, float vol)
    {
        if (clip == null || sfx == null) return;
        sfx.transform.position = pos;
        sfx.PlayOneShot(clip, vol);
    }

    void Say(string key, float seconds)
    {
        if (string.IsNullOrEmpty(key)) return;
        SubtitleManager.ShowSubtitle(new LocalizedString("Subtitles", key), false, null, seconds);
    }

    void StartMusic()
    {
        if (musicSource == null || music == null) return;
        // whatever plays on the Music group (the gameplay music) steps aside until it's over
        pausedMusic.Clear(); pausedVolume.Clear();
        foreach (var a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (a == musicSource || !a.isPlaying || a.outputAudioMixerGroup == null || musicGroup == null) continue;
            if (a.outputAudioMixerGroup != musicGroup && a.outputAudioMixerGroup.name != musicGroup.name) continue;
            pausedMusic.Add(a); pausedVolume[a] = a.volume;
            StartCoroutine(Co_FadeAndPause(a));
        }
        musicSource.clip = music;
        musicSource.volume = 0f;
        musicSource.Play();
        StartCoroutine(Co_Fade(musicSource, 0f, musicVolume, 0.8f, false));
    }

    void StopMusic()
    {
        if (musicSource != null && musicSource.isPlaying) StartCoroutine(Co_Fade(musicSource, musicSource.volume, 0f, 3f, true));
        StartCoroutine(Co_ResumeGameMusic());
    }

    IEnumerator Co_FadeAndPause(AudioSource a)
    {
        yield return Co_Fade(a, a.volume, 0f, 0.8f, false);
        if (a != null) a.Pause();
    }

    IEnumerator Co_ResumeGameMusic()
    {
        yield return new WaitForSeconds(2.5f);
        foreach (var a in pausedMusic)
        {
            if (a == null) continue;
            a.UnPause();
            StartCoroutine(Co_Fade(a, 0f, pausedVolume.TryGetValue(a, out float v) ? v : 1f, 3f, false));
        }
        pausedMusic.Clear();
    }

    IEnumerator Co_Fade(AudioSource s, float from, float to, float seconds, bool stopAtEnd)
    {
        for (float e = 0f; e < seconds && s != null; e += Time.deltaTime)
        {
            s.volume = Mathf.Lerp(from, to, e / seconds);
            yield return null;
        }
        if (s == null) yield break;
        s.volume = to;
        if (stopAtEnd) s.Stop();
    }

    // ------------------------------------------------------------------ skipping (the ending only)

    void HandleSkip()
    {
        var prompt = CutsceneSkipper.Instance;
        float holdTime = prompt != null ? prompt.HoldDuration : 1.5f;
        bool held = (Keyboard.current != null && Keyboard.current.eKey.isPressed) || (Gamepad.current != null && Gamepad.current.startButton.isPressed);
        if (held) skipHeld += Time.unscaledDeltaTime; else skipHeld = Mathf.Max(0f, skipHeld - Time.unscaledDeltaTime * 2f);
        if (prompt != null)
        {
            if (held || (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)) prompt.ShowHint();
            prompt.SetHoldProgress(skipHeld / holdTime);
        }
        if (skipHeld >= holdTime) StartCoroutine(Co_Skip());
    }

    void OnDisable()
    {
        if (Active)
        {
            // going away mid-way (scene unload / stop): no coroutines, just give him back
            var player = Player.instance;
            if (pausedByHint && player != null) player.pauseMovement = false;
            pausedByHint = false;
            ReleaseKhalid(player);
            EndChaseState(player, true);
            if (brain != null && blendSaved) brain.m_DefaultBlend = savedBlend;
            phase = Phase.Done;
        }
        IsPlaying = false;
        if (fogNow >= 0f && EnviroSkyLite.instance != null) EnviroSkyLite.instance.fogSettings.heightDensity = fogBase;
        fogNow = -1f; fogGoal = -1f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.DrawCube(areaCenter, areaSize);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(areaCenter, areaSize);
        Gizmos.color = Color.green;
        foreach (var s in spawns)
        {
            Gizmos.DrawSphere(s.hide, 0.6f);
            Vector3 a = s.hide;
            foreach (var p in s.leadIn) { Gizmos.DrawLine(a, p); a = p; }
        }
        Gizmos.color = Color.yellow;
        for (int i = 1; i < route.Count; i++) Gizmos.DrawLine(route[i - 1], route[i]);
    }

    // ------------------------------------------------------------------ debug

    /// <summary>Debug: Khalid just inside the area level with spawn 'index' (Play mode), and it plays.</summary>
    public void DebugPlayNow(int index)
    {
        var player = Player.instance;
        if (!Application.isPlaying || player == null || Active || spawns.Count == 0) return;
        Vector3 s = spawns[Mathf.Clamp(index, 0, spawns.Count - 1)].hide;
        Vector3 at = Ground(new Vector3(areaCenter.x - areaSize.x * 0.5f + 1.5f, s.y, s.z));
        if (player.controller != null) player.controller.enabled = false;
        player.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 90f, 0f));
        if (player.controller != null) player.controller.enabled = true;
        player.OnPlayerTelported?.Invoke();
        plankState = PlankState.Unset;
        phase = Phase.Waiting;
        Begin(true);
    }

    bool debugArmed;
    // just outside the area's near edge, one per way in (car lot, between the blocks, the sand lot, the far road)
    static readonly Vector3[] DebugJumpSpots =
    {
        new Vector3(-123f, 1.5f, -17f), new Vector3(-123f, 1.5f, -45.5f), new Vector3(-123f, 1.5f, -54f), new Vector3(-123f, 1.5f, -65f)
    };
    public int DebugJumpCount => DebugJumpSpots.Length;

    /// <summary>Debug (test key): stop whatever is playing, put it all back (plank across, Runner hidden behind its car, not
    /// played yet) and stand Khalid a few steps short of the area at jump spot 'index', facing it - walk in to play it.</summary>
    public void DebugResetAndJump(int index)
    {
        var player = Player.instance;
        if (!Application.isPlaying || player == null) return;
        StopAllCoroutines();
        if (Active)
        {
            if (pausedByHint) player.pauseMovement = false;
            pausedByHint = false; hintUntil = -1f;
            ReleaseKhalid(player);
            EndChaseState(player, true);
            if (musicSource != null) musicSource.Stop();
            foreach (var a in pausedMusic) if (a != null) { a.UnPause(); if (pausedVolume.TryGetValue(a, out float v)) a.volume = v; }
            pausedMusic.Clear();
        }
        if (noticeCam != null) noticeCam.Priority = 0;
        if (followCam != null) followCam.Priority = 0;
        if (brain != null && blendSaved) brain.m_DefaultBlend = savedBlend;
        restoreBlendAt = -1f;
        if (runner != null) runner.Vanish();
        if (adel != null) adel.gameObject.SetActive(false);
        if (steps != null) steps.End();
        if (CutsceneSkipper.Instance != null) CutsceneSkipper.Instance.HideHint();
        skipping = returning = false; IsPlaying = false; turnKhalid = false;
        fogGoal = -1f;
        debugArmed = true;
        plankState = PlankState.Unset;
        UpdateIdleState();

        Vector3 at = Ground(DebugJumpSpots[Mathf.Abs(index) % DebugJumpSpots.Length]);
        if (player.controller != null) player.controller.enabled = false;
        player.transform.SetPositionAndRotation(at + Vector3.up * 0.05f, Quaternion.Euler(0f, 90f, 0f));
        if (player.controller != null) player.controller.enabled = true;
        player.OnPlayerTelported?.Invoke();
    }

    public void DebugSkip() { if (phase == Phase.Ending && !skipping) StartCoroutine(Co_Skip()); }
    public string DebugState => phase + " " + beat + " t " + t.ToString("F1") + " s " + runnerS.ToString("F1") + "/" + shoveS.ToString("F1")
                                + " player s " + playerS.ToString("F1") + " plank " + plankState + " hint " + (hintUntil >= 0f);
    public int SpawnCount => spawns.Count;
    /// <summary>Debug/tests: the Runner's position along its line and where the plank spot is.</summary>
    public Vector3 DebugRunnerPos => runner != null ? runner.transform.position : Vector3.zero;
    public bool DebugInChase => phase == Phase.Chase;
}
