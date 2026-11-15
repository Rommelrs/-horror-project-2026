using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Localization;

/// <summary>
/// After the store (Game scene): Khalid comes down the lane of dead monsters. ONE shot down the lane (the user's framing):
/// Adel walks out from the park toilet onto the walkway, a Runner bursts in from behind him, throws him over its shoulder
/// and runs off. "Hey!! Help me!!" - "Adel!!" (same shot, no cut to Khalid). Then the chase: the Runner carries Adel along
/// the park walkways (the user's drawn line) to the closed Block 1 street gate, vaults it and is gone; it stays ~10 m
/// ahead; Khalid runs on the fast mocap run (faster cadence, a bit faster than his sprint), no pipe or gun. While it lasts
/// the sandstorm thickens (denser height fog, wind + blowing sand) and the table-knockdown music plays.
///
/// PLACEHOLDER animation (user: "simple placeholders, no polish"): Adel is a rigid model laid over the Runner's shoulder
/// playing his run clip (kicking), the Runner's grab is its Attack clip. The cutscene is a function of its time t like
/// StorageMeetingCutscene. Built by Tools > Cutscenes > Build Kidnap Sequence (Game scene).
/// </summary>
[DefaultExecutionOrder(150)]
public class KidnapSequence : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }
    public static bool ChaseActive { get; private set; }
    public const string SaveKey = "game:kidnap-adel";      // older builds' key (still honoured)
    /// <summary>StoryFlags flag: kept in saves/checkpoints AND as a session copy, so a death that rolls the save back to an
    /// older checkpoint doesn't play the kidnap again.</summary>
    public const string DoneFlag = "KidnapAdelDone";

    static bool AlreadyDone => StoryFlags.Has(DoneFlag) || (SaveManager.instance != null && SaveManager.instance.IsInteractableUsed(SaveKey));

    [Header("Actors")]
    [SerializeField] RunnerPuppet runner;
    [SerializeField] Animator adel;                  // Adel's model (Adel_Kidnap.controller: Idle / Walk (looping) / Run)

    [Header("The shot (down the lane, the user's framing)")]
    [SerializeField] Vector3 shotPos = new Vector3(-229.82f, 3.92f, 75.69f);
    [SerializeField] float shotYaw = 190.8f, shotPitch = 0.1f, shotFov = 60f;
    [Tooltip("The lens closes to this while Adel walks into the shot.")]
    [SerializeField] float zoomFov = 36f;
    [Tooltip("How much the camera turns after the action (0 = locked off).")]
    [Range(0f, 1f)] [SerializeField] float follow = 0.5f;

    [Header("Marks (world, y is found on the ground)")]
    [Tooltip("Adel's walk: out of the men's door, down the steps, east along the walkway past the lane.")]
    [SerializeField] List<Vector3> adelWalk = new List<Vector3>
    {
        new Vector3(-237.4f, 3.1f, 67.8f), new Vector3(-235.4f, 2.9f, 66.4f), new Vector3(-222f, 2.9f, 66.4f)
    };
    [SerializeField] Vector3 runnerStart = new Vector3(-243.5f, 2.9f, 66.3f);
    [Tooltip("Khalid during the shot (behind the camera, out of frame), facing down the lane.")]
    [SerializeField] Vector3 khalidMark = new Vector3(-229.7f, 2.75f, 77.4f);
    [Tooltip("The chase line after the grab (the first point is wherever the grab happens).")]
    [SerializeField] List<Vector3> path = new List<Vector3>();
    [Tooltip("Index in 'path' of the point the Runner LANDS on after vaulting the closed Block 1 street gate (the segment before it is " +
             "the jump; -1 = no vault). Khalid can't follow: he watches it go through the bars.")]
    [SerializeField] int vaultSegment = -1;
    [SerializeField] float vaultHeight = 3.4f;
    [SerializeField] float vaultSeconds = 0.85f;
    [Tooltip("Khalid reaching this point (radius) ends the chase.")]
    [SerializeField] Vector3 chaseEnd = new Vector3(-177f, 1.5f, 14f);
    [SerializeField] float chaseEndRadius = 8f;

    [Header("Speeds (user: animations faster, movement slower)")]
    [Tooltip("Khalid's sprint while chasing (normal sprint 3.2). The locomotion tree switches to the fast mocap run above 3.25.")]
    [SerializeField] float chaseSprintSpeed = 3.8f;
    [SerializeField] float gapWanted = 9.5f;
    [SerializeField] float runnerMinSpeed = 3.0f, runnerMaxSpeed = 5.5f;
    [Tooltip("Runner speed while carrying him off in the cutscene / after the gate.")]
    [SerializeField] float runnerCarrySpeed = 3.9f, runnerEscapeSpeed = 4.4f;
    [Tooltip("Further ahead than this (m along the line) he is lost in the fog: he waits there until Khalid comes closer.")]
    [SerializeField] float waitBeyondGap = 14f;

    [Header("Falling behind (user: camera to the runner + \"I better chase him!\")")]
    [Tooltip("The Runner has stood waiting (Khalid hardly moving) this long -> the camera turns to it and Khalid says the line.")]
    [SerializeField] float hintAfterWaiting = 3.5f;
    [SerializeField] float hintSeconds = 2.4f, hintCooldown = 15f, hintFov = 32f;
    [SerializeField] string hintLine = "I better chase him!";
    [Tooltip("Khalid further than this (m, straight line) from the Runner for farSeconds -> the reminder, whatever he is doing: " +
             "he stops, turns to face it and the camera shows it.")]
    [SerializeField] float farDistance = 20f, farSeconds = 1.2f, farCooldown = 6f;
    [Tooltip("Beyond this he is running off the wrong way: the reminder comes again as soon as the last one ends.")]
    [SerializeField] float leashDistance = 28f;
    [SerializeField] float lineCooldown = 10f;
    [Tooltip("Enviro height fog while the zoom holds: the storm parts so he shows at 20-30 m.")]
    [SerializeField] float hintFogDensity = 0.3f;
    [Tooltip("Ground speed the Runner's run clip is played for at animator speed 1 (lower = quicker legs for the same speed).")]
    [SerializeField] float runnerClipSpeed = 3.2f;
    [SerializeField] float adelWalkSpeed = 1.1f;

    [Header("Sandstorm while it lasts")]
    [Tooltip("Enviro height fog density during the kidnap and the chase (Game scene normally ~0.7).")]
    [SerializeField] float stormFogDensity = 1.35f;
    [Tooltip("The storm whips up as the Runner bursts in (Adel walks out in the normal haze), over stormRampIn seconds.")]
    [SerializeField] float stormRampIn = 5f, stormRampOut = 6f;
    [SerializeField] AudioClip windLoop;
    [SerializeField] float windVolume = 0.55f;
    [Tooltip("Blowing sand: the wind loop again, high-passed and pitched up into a hiss.")]
    [SerializeField] float sandVolume = 0.4f;

    [Header("Music (the table knockdown's)")]
    [SerializeField] AudioClip music;
    [SerializeField] AudioMixerGroup musicGroup;
    [SerializeField] float musicVolume = 0.5f;

    [Header("After the chase")]
    [SerializeField] MapLocationReveal reveal;       // none (the candy wrapper's reveal marks the barber)
    [SerializeField] MapHintPrompt mapHint;

    [Header("Sound")]
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField] AudioClip doorOpen, runnerScreech;

    [Header("Lines (Subtitles table keys)")]
    [SerializeField] string adelLine = "Hey!! Help me!!";
    [SerializeField] string khalidLine = "Adel!!";

    // ---- timing (seconds from the start of the cutscene)
    const float TAdelOut = 0.4f, ZoomStart = 1.2f, ZoomEnd = 4.2f, TRunIn = 3.4f, TGrab = 5.2f, TLifted = 5.7f;
    const float TAdelLine = 5.4f, TKhalidLine = 7.4f, TEnd = 9.2f;

    enum Phase { Waiting, Cutscene, Chase, Done }
    Phase phase = Phase.Waiting;
    float t, prevT, skipHeld;
    bool skipping;

    // path (with ground heights) and where the runner is on it
    readonly List<Vector3> pts = new List<Vector3>();
    readonly List<float> cum = new List<float>();
    float runnerS, runnerY, playerS;
    bool runnerOnPath, runnerGone, runnerWaiting;
    // falling behind
    CinemachineVirtualCamera hintShot;
    float waitingTime, lastHint = -999f, hintUntil = -1f, restoreBlendAt = -1f;
    // the reminder is a ZOOM from Khalid's side onto the Runner (user: "dont make it like some sort of cut, make it zoom on the
    // runner location"): the camera eases from the gameplay view to a spot by Khalid (lifted until it can see over walls) and
    // the lens closes on him; the storm thins a little meanwhile so he reads at 20-30 m
    Vector3 hintOffset;          // camera spot relative to Khalid, chosen when the reminder starts
    float hintStart, hintFog;
    bool hintTurnsKhalid, pausedByHint;
    float farTime, lastLine = -999f;
    Vector3 lastPlayerPos;
    CinemachineBlendDefinition gameplayBlend;

    // khalid
    Animator khalidAnim;
    readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();
    float savedSprint; bool savedStamina, savedWeapon;
    PlayerWeaponEnabler gunEnabler;

    // adel
    Transform adelHips, runnerShoulder;
    float adelHipHeight = 0.6f;
    bool adelLifted;
    Vector3 liftFromPos; Quaternion liftFromRot;

    // camera / sound / storm
    CinemachineVirtualCamera shot;
    CinemachineBrain brain;
    CinemachineBlendDefinition savedBlend;
    AudioSource sfx, wind, sand, musicSource;
    readonly List<AudioSource> pausedMusic = new List<AudioSource>();
    readonly Dictionary<AudioSource, float> pausedVolume = new Dictionary<AudioSource, float>();
    ScriptedFootsteps steps;
    bool lineAdelSaid, lineKhalidSaid, doorPlayed, screechPlayed, grabPlayed, musicStarted;
    float storm, stormTarget, fogBase = -1f;

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
        sfx = NewSource("Kidnap Audio", sfxGroup, 0.75f, false);
        sfx.rolloffMode = AudioRolloffMode.Linear; sfx.minDistance = 3f; sfx.maxDistance = 40f;
        wind = NewSource("Storm Wind", sfxGroup, 0f, true);
        sand = NewSource("Storm Sand", sfxGroup, 0f, true);
        sand.pitch = 1.3f;
        var hp = sand.gameObject.AddComponent<AudioHighPassFilter>(); hp.cutoffFrequency = 2600f; hp.highpassResonanceQ = 1f;
        musicSource = NewSource("Kidnap Music", musicGroup, 0f, true);

        var go = new GameObject("Shot down the lane");
        go.transform.SetParent(transform, false);
        shot = go.AddComponent<CinemachineVirtualCamera>();
        shot.m_Lens.FieldOfView = shotFov; shot.m_Lens.NearClipPlane = 0.08f;
        shot.Priority = 0;
        shot.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;

        var hgo = new GameObject("Chase hint (look at the Runner)");
        hgo.transform.SetParent(transform, false);
        hintShot = hgo.AddComponent<CinemachineVirtualCamera>();
        hintShot.m_Lens.FieldOfView = hintFov; hintShot.m_Lens.NearClipPlane = 0.08f;
        hintShot.Priority = 0;
        hintShot.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
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

    void Start()
    {
        if (AlreadyDone) phase = Phase.Done;
        HideActors();
    }

    void HideActors()
    {
        if (runner != null) runner.gameObject.SetActive(false);
        if (adel != null) adel.gameObject.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (phase != Phase.Waiting || !other.CompareTag("Player")) return;
        // a checkpoint / save restore may have arrived after Start: ask again
        if (AlreadyDone) { phase = Phase.Done; HideActors(); return; }
        Begin();
    }

    // ------------------------------------------------------------------ cutscene

    public void Begin() => Begin(false);

    void Begin(bool debugForce)
    {
        var player = Player.instance;
        if (player == null || phase != Phase.Waiting) return;
        if (AlreadyDone && !debugForce) { phase = Phase.Done; HideActors(); return; }
        phase = Phase.Cutscene; IsPlaying = true; t = prevT = 0f; skipHeld = 0f; skipping = false;
        lineAdelSaid = lineKhalidSaid = doorPlayed = screechPlayed = grabPlayed = musicStarted = runnerWaiting = false;
        stormTarget = 0f;
        adelLifted = false; runnerOnPath = false; runnerGone = false;
        StoryFlags.Set(DoneFlag);

        PlayerMeleeWeapon.SetCutscene(PlayerMeleeWeapon.CutsceneHold.Away);
        if (player.playerWeaponSystem != null) player.playerWeaponSystem.ExitOutOfAiming();
        Disable(player.GetComponent<PlayerMovement>());
        Disable(player.playerWeaponSystem);
        Disable(player.GetComponent<PlayerRoll>());
        Disable(player.GetComponent<PlayerMeleeAttack>());
        Disable(player.GetComponent<DoorInteractionHandler>());
        if (player.controller != null) player.controller.enabled = false;
        if (player.health != null) player.health.Invincible = true;
        HideGun(player, true);

        // Khalid stands behind the camera, looking down the lane
        player.transform.SetPositionAndRotation(Ground(khalidMark), Quaternion.Euler(0f, shotYaw, 0f));
        khalidAnim = null;
        foreach (var a in player.GetComponentsInChildren<Animator>(true)) if (a.name == "MainCharacterAnimated") khalidAnim = a;
        if (khalidAnim != null)
        {
            khalidAnim.SetFloat("Velocity", 0f); khalidAnim.SetFloat("x", 0f); khalidAnim.SetFloat("y", 0f); khalidAnim.SetBool("Turning", false);
            khalidAnim.Play("WalkMove", 0, 0f);
        }

        if (adel != null)
        {
            adel.gameObject.SetActive(true);
            if (adelHips != null) adelHipHeight = Mathf.Max(0.3f, adelHips.position.y - adel.transform.position.y);
            PlaceAdelWalking(0f);
            adel.gameObject.SetActive(false);              // out of the door at TAdelOut
        }
        if (runner != null) runner.gameObject.SetActive(false);

        if (steps == null) steps = gameObject.AddComponent<ScriptedFootsteps>();
        steps.End();

        var cam = Camera.main;
        brain = cam != null ? cam.GetComponent<CinemachineBrain>() : null;
        if (brain != null) { savedBlend = brain.m_DefaultBlend; brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f); }
        Evaluate();
    }

    void Disable(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false; disabledBehaviours.Add(b);
    }

    void Update()
    {
        if (phase == Phase.Cutscene)
        {
            if (!skipping)
            {
                prevT = t;
                t += Time.deltaTime;
                Evaluate();
                if (t >= TEnd) FinishCutscene();
                else HandleSkip();
            }
        }
        else if (phase == Phase.Chase) UpdateChase(Time.deltaTime);
        else if (phase == Phase.Done && runnerOnPath && !runnerGone) AdvanceRunner(Time.deltaTime, false);
        UpdateStorm(Time.deltaTime);
    }

    void LateUpdate()
    {
        if (hintUntil >= 0f) UpdateHintShot(false);
        if (hintUntil >= 0f && hintTurnsKhalid && Player.instance != null && runner != null)
        {
            Vector3 d = Flat(runner.transform.position - Player.instance.transform.position);
            if (d.sqrMagnitude > 0.01f)
                Player.instance.transform.rotation = Quaternion.RotateTowards(Player.instance.transform.rotation, Quaternion.LookRotation(d), 240f * Time.deltaTime);
        }
        if (phase == Phase.Cutscene || phase == Phase.Chase || (phase == Phase.Done && !runnerGone)) PoseAdelCarried();
        // Enviro eases the fog back to its weather preset every frame: ours goes on top while the storm lasts
        if (fogBase >= 0f && EnviroSkyLite.instance != null)
        {
            hintFog = Mathf.MoveTowards(hintFog, hintUntil >= 0f ? 1f : 0f, Time.deltaTime / 0.8f);
            float dens = Mathf.Lerp(fogBase, stormFogDensity, storm);
            EnviroSkyLite.instance.fogSettings.heightDensity = Mathf.Lerp(dens, Mathf.Min(dens, hintFogDensity), Mathf.SmoothStep(0f, 1f, hintFog));
            if (storm <= 0f && stormTarget <= 0f) fogBase = -1f;   // done: Enviro has it back (already at its own value)
        }
    }

    bool Crossed(float time) => prevT < time && t >= time;

    // ---- Adel's walk along adelWalk
    float AdelWalkLength
    {
        get { float l = 0f; for (int i = 1; i < adelWalk.Count; i++) l += Flat(adelWalk[i] - adelWalk[i - 1]).magnitude; return l; }
    }

    Vector3 AdelWalkPos(float walked, out Vector3 dir)
    {
        dir = Vector3.forward;
        if (adelWalk.Count == 0) return Vector3.zero;
        float s = Mathf.Max(0f, walked * adelWalkSpeed);
        for (int i = 1; i < adelWalk.Count; i++)
        {
            Vector3 seg = Flat(adelWalk[i] - adelWalk[i - 1]);
            float len = seg.magnitude;
            dir = seg.normalized;
            // turn into the next leg over the last half metre
            if (i < adelWalk.Count - 1 && s > len - 0.5f && s <= len)
                dir = Vector3.Slerp(dir, Flat(adelWalk[i + 1] - adelWalk[i]).normalized, (s - (len - 0.5f)) / 0.5f).normalized;
            if (s <= len || i == adelWalk.Count - 1) return Ground(Vector3.Lerp(adelWalk[i - 1], adelWalk[i], Mathf.Clamp01(s / Mathf.Max(0.0001f, len))));
            s -= len;
        }
        return Ground(adelWalk[adelWalk.Count - 1]);
    }

    void PlaceAdelWalking(float walked)
    {
        if (adel == null) return;
        Vector3 p = AdelWalkPos(walked, out Vector3 dir);
        adel.transform.SetPositionAndRotation(p, Quaternion.LookRotation(dir));
    }

    Vector3 GrabSpot => AdelWalkPos(TGrab - TAdelOut, out _);
    Vector3 GrabDir { get { AdelWalkPos(TGrab - TAdelOut, out Vector3 d); return d; } }
    Vector3 RunnerGrabPos => Ground(GrabSpot - GrabDir * 0.55f);

    void Evaluate()
    {
        var player = Player.instance;
        if (player == null) return;

        // ---- Adel walks out of the toilet (heard: the door) and along the walkway
        if (Crossed(TAdelOut - 0.3f) && !doorPlayed) { doorPlayed = true; PlayAt(doorOpen, adelWalk.Count > 0 ? adelWalk[0] : transform.position, 0.9f); }
        if (adel != null && t >= TAdelOut && !adelLifted)
        {
            if (!adel.gameObject.activeSelf) { adel.gameObject.SetActive(true); adel.Play("Walk", 0, 0f); steps.Track(adel, 0.55f); }
            float walked = Mathf.Min(t, TGrab) - TAdelOut;
            PlaceAdelWalking(walked);
            SetAdelState(walked * adelWalkSpeed < AdelWalkLength - 0.01f ? "Walk" : "Idle", 1.15f);
        }

        // ---- the Runner comes from behind him
        if (runner != null && t >= TRunIn && !runnerOnPath)
        {
            if (!runner.gameObject.activeSelf)
            {
                runner.Appear(Ground(runnerStart), GrabSpot);
                runner.Run();
                var ra0 = runner.GetComponentInChildren<Animator>();
                if (ra0 != null) steps.Track(ra0, 0.95f);
            }
            float u = Mathf.InverseLerp(TRunIn, TGrab, t);
            Vector3 from = Ground(runnerStart), to = RunnerGrabPos;
            runner.transform.position = Ground(Vector3.Lerp(from, to, u));
            runner.FaceInstant(to - from);
            SetRunnerSpeed(Vector3.Distance(from, to) / (TGrab - TRunIn));
            if (t >= TGrab) { StartPath(runner.transform.position); runnerOnPath = true; }
        }
        if (stormTarget <= 0f && t >= TRunIn - 0.4f) StartStorm();          // the sand whips up with him
        if (!musicStarted && t >= TRunIn + 0.6f) { musicStarted = true; StartMusic(); }
        if (!screechPlayed && t >= TGrab - 0.45f && runner != null && runner.gameObject.activeSelf) { screechPlayed = true; PlayAt(runnerScreech, runner.transform.position, 1f); }
        if (!grabPlayed && t >= TGrab - 0.25f && runner != null && runner.gameObject.activeSelf)
        {
            grabPlayed = true;
            var ra = runner.GetComponentInChildren<Animator>();
            if (ra != null) { ra.speed = 1.6f; ra.CrossFadeInFixedTime("Attack", 0.12f, 0, 0.35f); }
        }
        if (adel != null && t >= TGrab && !adelLifted)
        {
            adelLifted = true;
            liftFromPos = adel.transform.position; liftFromRot = adel.transform.rotation;
            SetAdelState("Run", 0.85f);                    // kicking and flailing on the shoulder
        }
        if (runnerOnPath && runner != null)
        {
            // he takes a moment to heave him up, then runs off along the line
            float runTime = Mathf.Max(0f, t - (TLifted - 0.1f));
            if (t >= TLifted - 0.1f)
            {
                var ra = runner.GetComponentInChildren<Animator>();
                if (ra != null && ra.GetCurrentAnimatorStateInfo(0).IsName("Attack") && !ra.IsInTransition(0)) ra.CrossFadeInFixedTime("Movement", 0.15f);
                runner.Run();
            }
            runnerS = Mathf.Min(PathLength, runnerCarrySpeed * runTime);
            PlaceRunnerOnPath(runnerCarrySpeed, Time.deltaTime);
        }

        if (Crossed(TAdelLine) && !lineAdelSaid) { lineAdelSaid = true; Say(adelLine, 2.0f); }
        if (Crossed(TKhalidLine) && !lineKhalidSaid) { lineKhalidSaid = true; Say(khalidLine, 1.7f); }

        UpdateShot();
    }

    void SetAdelState(string state, float speed)
    {
        if (adel == null) return;
        adel.speed = speed;
        var info = adel.GetCurrentAnimatorStateInfo(0);
        if (!info.IsName(state) && !adel.IsInTransition(0)) adel.CrossFadeInFixedTime(state, 0.2f);
    }

    void SetRunnerSpeed(float metresPerSecond)
    {
        var ra = runner != null ? runner.GetComponentInChildren<Animator>() : null;
        if (ra == null) return;
        if (ra.GetCurrentAnimatorStateInfo(0).IsName("Attack")) return;
        ra.speed = Mathf.Clamp(metresPerSecond / runnerClipSpeed, 0.9f, 1.6f);
    }

    /// <summary>Adel over the Runner's shoulder (fireman's carry): belly on the shoulder, head and arms down its back.</summary>
    void PoseAdelCarried()
    {
        if (!adelLifted || adel == null || runner == null || !adel.gameObject.activeSelf) return;
        Vector3 f = Flat(runner.transform.forward).normalized;
        Vector3 shoulder = runnerShoulder != null ? runnerShoulder.position : runner.transform.position + Vector3.up * 1.55f;
        Vector3 up = (-f + Vector3.down * 0.35f).normalized;                 // his head goes down behind its back
        Vector3 fwd = Vector3.ProjectOnPlane(Vector3.down, up).normalized;   // belly down onto the shoulder
        Quaternion rot = Quaternion.LookRotation(fwd, up);
        Vector3 pos = shoulder + Vector3.up * 0.12f + f * 0.05f - up * adelHipHeight;
        float u = phase == Phase.Cutscene ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TGrab, TLifted, t)) : 1f;
        adel.transform.SetPositionAndRotation(Vector3.Lerp(liftFromPos, pos, u), Quaternion.Slerp(liftFromRot, rot, u));
    }

    void FinishCutscene()
    {
        var player = Player.instance;
        // where everybody ends up (a skip jumps here)
        if (runner != null)
        {
            if (!runner.gameObject.activeSelf) runner.Appear(Ground(runnerStart), GrabSpot);
            if (!runnerOnPath) { StartPath(RunnerGrabPos); runnerOnPath = true; }
            var ra = runner.GetComponentInChildren<Animator>();
            if (ra != null) ra.CrossFadeInFixedTime("Movement", 0.1f);
            runner.Run();
            runnerS = Mathf.Max(runnerS, Mathf.Min(PathLength, runnerCarrySpeed * (TEnd - TLifted)));
            PlaceRunnerOnPath(runnerCarrySpeed, 0f);
        }
        if (adel != null)
        {
            if (!adel.gameObject.activeSelf) adel.gameObject.SetActive(true);
            if (!adelLifted) { adelLifted = true; liftFromPos = adel.transform.position; liftFromRot = adel.transform.rotation; }
            adel.speed = 0.85f; adel.Play("Run", 0, 0f);
        }
        if (stormTarget <= 0f) StartStorm();
        if (!musicStarted) { musicStarted = true; StartMusic(); }
        if (skipping && SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) SubtitleManager.instance.ForceClose();

        if (shot != null) shot.Priority = 0;
        if (brain != null) brain.m_DefaultBlend = savedBlend;
        if (player != null)
        {
            player.OnPlayerTelported?.Invoke();
            if (player.controller != null) player.controller.enabled = true;
            if (player.health != null) player.health.Invincible = false;
        }
        foreach (var b in disabledBehaviours) if (b != null) b.enabled = true;
        disabledBehaviours.Clear();
        if (khalidAnim != null) { khalidAnim.SetFloat("Velocity", 0f); khalidAnim.SetFloat("x", 0f); khalidAnim.SetFloat("y", 0f); }
        if (CutsceneSkipper.Instance != null) CutsceneSkipper.Instance.HideHint();
        IsPlaying = false; skipping = false;
        BeginChase();
    }

    // ------------------------------------------------------------------ chase

    void BeginChase()
    {
        var player = Player.instance;
        phase = Phase.Chase; ChaseActive = true;
        if (player == null) return;
        var pm = player.GetComponent<PlayerMovement>();
        if (pm != null)
        {
            savedSprint = pm.SprintSpeed; savedStamina = pm.useStaminaForSprint;
            pm.SprintSpeed = chaseSprintSpeed; pm.useStaminaForSprint = false;
        }
        // no pipe, no gun: he just runs
        PlayerMeleeWeapon.SetCutscene(PlayerMeleeWeapon.CutsceneHold.Away);
        HideGun(player, true);
        playerS = ProjectOnPath(player.transform.position, 0f);
        lastPlayerPos = player.transform.position; waitingTime = 0f; farTime = 0f; hintUntil = -1f; restoreBlendAt = -1f;
    }

    void UpdateChase(float dt)
    {
        var player = Player.instance;
        if (player == null) return;
        // the gun stays put away even if something switches it back on
        if (player.playerWeaponSystem != null && player.playerWeaponSystem.weaponIsEnabled) HideGun(player, false);

        playerS = ProjectOnPath(player.transform.position, playerS);
        AdvanceRunner(dt, true);
        UpdateFallingBehind(player, dt);

        Vector3 pe = Flat(player.transform.position - chaseEnd);
        if (pe.magnitude <= chaseEndRadius) EndChase();
    }

    /// <summary>Khalid hangs back while the Runner stands waiting for him: the camera swings over his shoulder to it and he says
    /// "I better chase him!" (no more often than hintCooldown). He keeps control the whole time.</summary>
    void UpdateFallingBehind(Player player, float dt)
    {
        Vector3 pos = player.transform.position;
        float speed = dt > 0f ? Flat(pos - lastPlayerPos).magnitude / dt : 0f;
        lastPlayerPos = pos;
        if (runnerWaiting && speed < 1.5f) waitingTime += dt; else waitingTime = 0f;
        float dist = runner != null ? Flat(runner.transform.position - pos).magnitude : 0f;
        if (dist > farDistance) farTime += dt; else farTime = 0f;
        bool canHint = hintUntil < 0f && runner != null && runner.gameObject.activeSelf;
        bool hangingBack = waitingTime >= hintAfterWaiting && Time.time - lastHint >= hintCooldown;
        bool tooFar = farTime >= farSeconds && Time.time - lastHint >= (dist > leashDistance ? hintSeconds + 1f : farCooldown);
        if (canHint && (hangingBack || tooFar))
        {
            lastHint = Time.time; hintUntil = Time.time + hintSeconds; waitingTime = 0f; farTime = 0f;
            if (brain == null && Camera.main != null) brain = Camera.main.GetComponent<CinemachineBrain>();
            // far away he'd be a speck in the storm over Khalid's shoulder: show him from close by instead
            hintOffset = HintCameraOffset(player);
            hintStart = Time.time;
            // wandering off: Khalid stops and turns to face it (he comes out of it pointing the right way)
            hintTurnsKhalid = tooFar;
            if (hintTurnsKhalid && !player.pauseMovement) { player.pauseMovement = true; pausedByHint = true; }
            if (brain != null)
            {
                if (restoreBlendAt < 0f) gameplayBlend = brain.m_DefaultBlend;
                // never a cut: the view eases over to the zoom and back
                brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.7f);
                restoreBlendAt = -1f;
            }
            UpdateHintShot(true);
            hintShot.Priority = 70;
            if (Time.time - lastLine >= lineCooldown) { lastLine = Time.time; Say(hintLine, 2.2f); }
        }
        if (hintUntil >= 0f && Time.time >= hintUntil) EndHint();
        if (restoreBlendAt >= 0f && Time.time >= restoreBlendAt) { if (brain != null) brain.m_DefaultBlend = gameplayBlend; restoreBlendAt = -1f; }
    }

    void EndHint()
    {
        if (pausedByHint && Player.instance != null) Player.instance.pauseMovement = false;
        pausedByHint = false; hintTurnsKhalid = false;
        if (hintShot != null) hintShot.Priority = 0;
        if (hintUntil >= 0f) restoreBlendAt = Time.time + 0.8f;     // blend back over the same 0.6 s, then the game's own blend again
        hintUntil = -1f;
    }

    Vector3 RunnerChest => runner.transform.position + Vector3.up * 1.3f;

    /// <summary>Anything solid between a and b, Khalid himself not counted.</summary>
    static bool Blocked(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        foreach (var h in Physics.RaycastAll(a, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (Player.instance == null || !h.collider.transform.IsChildOf(Player.instance.transform)) return true;
        return false;
    }

    /// <summary>The zoom: from Khalid's side (offset chosen at the start) onto the Runner, lens closing over ~1 s.</summary>
    void UpdateHintShot(bool snap)
    {
        var player = Player.instance;
        if (hintShot == null || player == null || runner == null) return;
        Vector3 target = RunnerChest;
        Vector3 pos = player.transform.position + hintOffset;
        Quaternion rot = Quaternion.LookRotation(target - pos, Vector3.up);
        hintShot.transform.SetPositionAndRotation(pos, snap ? rot : Quaternion.Slerp(hintShot.transform.rotation, rot, 1f - Mathf.Exp(-8f * Time.deltaTime)));
        // the lens closes on him: from a normal view down to ~4.5 m of frame height at his distance
        float dist = Vector3.Distance(pos, target);
        float tight = Mathf.Clamp(2f * Mathf.Atan(2.25f / Mathf.Max(1f, dist)) * Mathf.Rad2Deg, 9f, 40f);
        float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hintStart + 0.2f, hintStart + 1.1f, Time.time));
        hintShot.m_Lens.FieldOfView = Mathf.Lerp(50f, tight, u);
    }

    /// <summary>Where the zoom looks from, relative to Khalid: over his shoulder if that sees the Runner, otherwise lifted
    /// above him until it clears the walls (in the lane), else just over his head.</summary>
    Vector3 HintCameraOffset(Player player)
    {
        Vector3 k = player.transform.position;
        Vector3 head = k + Vector3.up * 1.7f;
        Vector3 target = RunnerChest;
        Vector3 to = Flat(target - head);
        if (to.sqrMagnitude < 0.01f) to = player.transform.forward;
        to.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, to);
        foreach (float lift in new[] { 0.2f, 1.2f, 2.5f, 4f, 6f })
        {
            Vector3 p = head - to * 1.3f + right * 0.45f + Vector3.up * lift;
            if (!Blocked(head, p) && !Blocked(p, target)) return p - k;
        }
        foreach (float lift in new[] { 1.5f, 3f, 5f })
        {
            Vector3 p = head + Vector3.up * lift;
            if (!Blocked(head, p) && !Blocked(p, target)) return p - k;
        }
        return head - to * 0.6f + Vector3.up * 0.4f - k;
    }

    /// <summary>The Runner along the line. chasing: kept ~gapWanted ahead of Khalid (waits in the fog if he falls far behind);
    /// otherwise (the chase is over, Khalid is at the gate) he just gets away over it.</summary>
    void AdvanceRunner(float dt, bool chasing)
    {
        if (runnerGone || runner == null || !runnerOnPath) return;
        float gap = runnerS - playerS;
        float sp = chasing ? Mathf.Clamp(chaseSprintSpeed + (gapWanted - gap) * 0.5f, runnerMinSpeed, runnerMaxSpeed) : runnerEscapeSpeed;
        bool beforeGate = vaultSegment <= 0 || runnerS < cum[Mathf.Min(cum.Count - 1, vaultSegment)];
        // lost in the fog far ahead: he waits for Khalid there (nobody can see him stand)
        float straight = Player.instance != null ? Flat(runner.transform.position - Player.instance.transform.position).magnitude : 0f;
        bool wait = chasing && beforeGate && (runnerWaiting ? gap > waitBeyondGap - 2f || straight > waitBeyondGap + 2f
                                                            : gap > waitBeyondGap || straight > waitBeyondGap + 4f);
        if (wait != runnerWaiting) { runnerWaiting = wait; if (wait) runner.Idle(); else runner.Run(); }
        if (wait) sp = 0f;
        // the jump over the gate at its own pace, and past it he just gets away
        if (InVault(runnerS, out _, out float vaultLen)) sp = vaultLen / vaultSeconds;
        else if (vaultSegment > 0 && runnerS >= cum[Mathf.Min(cum.Count - 1, vaultSegment + 1)]) sp = Mathf.Max(sp, runnerEscapeSpeed);
        runnerS = Mathf.Min(PathLength, runnerS + sp * dt);
        PlaceRunnerOnPath(wait ? runnerMinSpeed : sp, dt);
        // standing there with Adel on its shoulder, it turns to look back at him
        if (wait && Player.instance != null) runner.FaceDirection(Player.instance.transform.position - runner.transform.position, dt);
        if (runnerS >= PathLength - 0.05f)
        {
            // into the block and gone (out in the fog by now)
            runnerGone = true;
            if (steps != null) steps.End();
            runner.Vanish();
            if (adel != null) adel.gameObject.SetActive(false);
        }
    }

    void EndChase(bool disabling = false)
    {
        var player = Player.instance;
        EndHint();
        if (brain != null && restoreBlendAt >= 0f) { brain.m_DefaultBlend = gameplayBlend; restoreBlendAt = -1f; }
        phase = Phase.Done; ChaseActive = false;
        if (player != null)
        {
            var pm = player.GetComponent<PlayerMovement>();
            if (pm != null) { pm.SprintSpeed = savedSprint; pm.useStaminaForSprint = savedStamina; }
        }
        PlayerMeleeWeapon.SetCutscene(PlayerMeleeWeapon.CutsceneHold.None);
        if (disabling)
        {
            // going away (scene unload / stop): no coroutines - just give the gun back
            if (player != null && player.playerWeaponSystem != null)
                player.playerWeaponSystem.weaponIsEnabled = savedWeapon || (player.inventory != null && player.inventory.HasWeapon());
            return;
        }
        StartCoroutine(Co_GunBack());
        StartCoroutine(Co_AfterChase());
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
    }

    IEnumerator Co_AfterChase()
    {
        // checkpoint at the gate once he is himself again (speed, pipe/gun back): dying later, Continue or a reload come
        // back here with the kidnap done, never before it
        yield return null; yield return null; yield return null;
        if (CheckpointManager.instance != null) CheckpointManager.instance.TriggerCheckpoint("Adel taken");

        // let the runner get out of sight first
        float wait = 0f;
        while (!runnerGone && wait < 6f) { wait += Time.deltaTime; yield return null; }
        yield return new WaitForSeconds(0.8f);
        if (runner != null && !runnerGone) { runner.Vanish(); if (adel != null) adel.gameObject.SetActive(false); runnerGone = true; }
        StopStorm();
        if (reveal != null) reveal.TriggerReveal();
        if (mapHint != null) mapHint.ShowHint();
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

    // ------------------------------------------------------------------ storm + music

    void StartStorm()
    {
        stormTarget = 1f;
        if (EnviroSkyLite.instance != null && fogBase < 0f) fogBase = EnviroSkyLite.instance.fogSettings.heightDensity;
        foreach (var s in new[] { wind, sand })
        {
            if (s == null || windLoop == null) continue;
            s.clip = windLoop;
            if (!s.isPlaying) { s.time = Random.Range(0f, windLoop.length * 0.8f); s.Play(); }
        }
    }

    void StopStorm()
    {
        stormTarget = 0f;
        if (musicSource != null && musicSource.isPlaying) StartCoroutine(Co_Fade(musicSource, musicSource.volume, 0f, 3f, true));
        StartCoroutine(Co_ResumeGameMusic());
    }

    void UpdateStorm(float dt)
    {
        float rate = stormTarget > storm ? 1f / Mathf.Max(0.01f, stormRampIn) : 1f / Mathf.Max(0.01f, stormRampOut);
        storm = Mathf.MoveTowards(storm, stormTarget, rate * dt);
        if (wind == null || sand == null) return;
        // gusts: slow noise on the wind, a faster one on the sand
        float gust = Mathf.PerlinNoise(Time.time * 0.23f, 0.3f);
        float hiss = Mathf.PerlinNoise(Time.time * 0.6f, 7.1f);
        wind.volume = storm * windVolume * Mathf.Lerp(0.6f, 1.15f, gust);
        sand.volume = storm * sandVolume * Mathf.Lerp(0.35f, 1.1f, hiss * gust + 0.3f * hiss);
        if (storm <= 0f && stormTarget <= 0f) { if (wind.isPlaying) wind.Stop(); if (sand.isPlaying) sand.Stop(); }
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

    // ------------------------------------------------------------------ path

    float PathLength => cum.Count > 0 ? cum[cum.Count - 1] : 0f;

    void StartPath(Vector3 start)
    {
        pts.Clear(); cum.Clear();
        pts.Add(start);
        foreach (var p in path) pts.Add(Ground(p));
        cum.Add(0f);
        for (int i = 1; i < pts.Count; i++) cum.Add(cum[i - 1] + Flat(pts[i] - pts[i - 1]).magnitude);
        runnerS = 0f; runnerY = start.y;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

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
        // round the corners a little: blend toward the next segment's heading over the last 1.5 m
        if (i < pts.Count - 1)
        {
            float toEnd = cum[i] - s;
            if (toEnd < 1.5f) dir = Vector3.Slerp(dir, Flat(pts[i + 1] - pts[i]).normalized, 0.5f * (1f - toEnd / 1.5f)).normalized;
        }
        return Vector3.Lerp(pts[i - 1], pts[i], u);
    }

    void PlaceRunnerOnPath(float speed, float dt)
    {
        if (runner == null || pts.Count < 2) return;
        Vector3 p = PathPoint(runnerS, out Vector3 dir);
        if (InVault(runnerS, out float u, out _))
        {
            int a = vaultSegment;
            runnerY = Mathf.Lerp(pts[a].y, pts[a + 1].y, u) + Mathf.Sin(Mathf.PI * u) * vaultHeight;
        }
        else
        {
            float gy = Ground(p).y;
            runnerY = dt > 0f ? Mathf.Lerp(runnerY, gy, 1f - Mathf.Exp(-12f * dt)) : gy;
        }
        p.y = runnerY;
        runner.transform.position = p;
        if (dt > 0f) runner.FaceDirection(dir, dt); else runner.FaceInstant(dir);
        SetRunnerSpeed(speed);
    }

    /// <summary>Is arc length s on the jump over the gate (pts[vaultSegment] -> pts[vaultSegment + 1]; pts[0] is the grab spot).</summary>
    bool InVault(float s, out float u, out float length)
    {
        u = 0f; length = 0f;
        int a = vaultSegment;
        if (a < 1 || a + 1 >= pts.Count) return false;
        length = cum[a + 1] - cum[a];
        if (s < cum[a] || s > cum[a + 1] || length <= 0f) return false;
        u = (s - cum[a]) / length;
        return true;
    }

    /// <summary>Arc length of the point on the line closest to 'pos', searched around the last answer so it never jumps far.</summary>
    float ProjectOnPath(Vector3 pos, float near)
    {
        if (pts.Count < 2) return 0f;
        float best = near, bestD = float.MaxValue;
        for (int i = 1; i < pts.Count; i++)
        {
            if (cum[i] < near - 25f || cum[i - 1] > near + 40f) continue;
            Vector3 a = Flat(pts[i - 1]), b = Flat(pts[i]), q = Flat(pos);
            Vector3 ab = b - a;
            float u = Mathf.Clamp01(Vector3.Dot(q - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            float d = (a + ab * u - q).sqrMagnitude;
            if (d < bestD) { bestD = d; best = cum[i - 1] + (cum[i] - cum[i - 1]) * u; }
        }
        return best;
    }

    Vector3 Ground(Vector3 p)
    {
        var hits = Physics.RaycastAll(new Vector3(p.x, p.y + 2.5f, p.z), Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
        float bestY = float.NegativeInfinity;
        foreach (var h in hits)
        {
            if (Player.instance != null && h.collider.transform.IsChildOf(Player.instance.transform)) continue;
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

    // ------------------------------------------------------------------ the shot

    /// <summary>The user's framing down the lane; the lens closes in as Adel walks out and the camera leans a little after the
    /// action (Adel, then the Runner carrying him off).</summary>
    void UpdateShot()
    {
        if (shot == null) return;
        Vector3 action;
        if (runner != null && runner.gameObject.activeSelf && t >= TGrab) action = runner.transform.position + Vector3.up * 1.3f;
        else if (adel != null && adel.gameObject.activeSelf) action = adel.transform.position + Vector3.up * 0.9f;
        else action = shotPos + Quaternion.Euler(shotPitch, shotYaw, 0f) * Vector3.forward * 10f;
        Quaternion baseRot = Quaternion.Euler(shotPitch, shotYaw, 0f);
        float w = follow * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ZoomStart, ZoomEnd, t));
        Quaternion toAction = Quaternion.LookRotation(action - shotPos, Vector3.up);
        Quaternion rot = Quaternion.Slerp(baseRot, toAction, w);
        // ease it so it drifts, not snaps, when the subject changes
        shot.transform.SetPositionAndRotation(shotPos, t <= 0f ? rot : Quaternion.Slerp(shot.transform.rotation, rot, 1f - Mathf.Exp(-4f * Time.deltaTime)));
        shot.m_Lens.FieldOfView = Mathf.Lerp(shotFov, zoomFov, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ZoomStart, ZoomEnd, t)));
        shot.Priority = 60;
    }

    // ------------------------------------------------------------------ skipping

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

    IEnumerator Co_Skip()
    {
        skipping = true;
        if (FadeScreenUI.instance != null) FadeScreenUI.instance.FadeOut(0.35f);
        yield return new WaitForSecondsRealtime(0.4f);
        t = TEnd;
        FinishCutscene();
        if (FadeScreenUI.instance != null) FadeScreenUI.instance.FadeIn(0.5f);
    }

    void OnDisable()
    {
        if (phase == Phase.Chase) EndChase(true);
        if (IsPlaying) IsPlaying = false;
        ChaseActive = false;
        if (fogBase >= 0f && EnviroSkyLite.instance != null) EnviroSkyLite.instance.fogSettings.heightDensity = fogBase;
    }

    // ------------------------------------------------------------------ debug

    /// <summary>Debug: play it now from the lane (Play mode).</summary>
    [ContextMenu("Debug: Play Now")]
    public void DebugPlayNow()
    {
        var player = Player.instance;
        if (!Application.isPlaying || player == null || phase == Phase.Cutscene) return;
        phase = Phase.Waiting;
        if (player.controller != null) player.controller.enabled = false;
        player.transform.SetPositionAndRotation(Ground(transform.position), Quaternion.Euler(0f, 180f, 0f));
        if (player.controller != null) player.controller.enabled = true;
        player.OnPlayerTelported?.Invoke();
        Begin(true);
    }

    public void DebugSkip() { if (phase == Phase.Cutscene && !skipping) StartCoroutine(Co_Skip()); }
    public void DebugSetTime(float time) { if (phase == Phase.Cutscene) { prevT = time; t = time; } }
    public float DebugTime => t;
    public string DebugPhase => phase.ToString();
    public float DebugGap => runnerS - playerS;
    public float DebugRunnerS => runnerS;
    public float DebugPathLength => PathLength;
    public float DebugStorm => storm;
}
