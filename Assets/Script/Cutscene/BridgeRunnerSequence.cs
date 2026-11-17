using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

/// <summary>
/// After the knife handle (Alley 1), on the way north (Game scene): Khalid crosses the user's drawn line and the camera finds
/// the Runner with Adel still over its shoulder, out on the main road at whichever marked spot is nearest to where Khalid came
/// in. It runs up the side road and over the plank laid across the ravine; on the far side it turns and shoves the plank down
/// into the ravine, looks back, and is gone into the fog. Khalid has to find another way round (the plank on the far side is
/// still there for later).
///
/// No cuts (user: zoom on the Runner): the view eases from Khalid onto the Runner (lens closing), sweeps to a camera trailing
/// the Runner that stops at the near edge of the ravine, and eases back to Khalid at the end. Hold E skips.
///
/// Saves: plays once. The done flag (StoryFlags, kept through a death rolling the save back) is set when it starts, and a
/// checkpoint is written when Khalid has control again. The plank is on the bridge only between the knife handle pickup and
/// this; afterwards it lies in the ravine (also after a reload). Built by Tools > Cutscenes > Build Bridge Runner Sequence.
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
    [SerializeField] Vector3 areaCenter = new Vector3(-110.15f, 4f, -23.5f);
    [SerializeField] Vector3 areaSize = new Vector3(41.7f, 20f, 97f);
    [Tooltip("Safety net: Khalid further north than this (x) while it is still waiting = he got round it; it's marked done quietly.")]
    [SerializeField] float northOfRoadX = -100f;

    [Header("Actors")]
    [SerializeField] RunnerPuppet runner;
    [SerializeField] Animator adel;                  // Adel_Kidnap.controller (Idle / Walk / Run)
    [Tooltip("The plank across the ravine (copy of the bridge's own repair plank).")]
    [SerializeField] Transform plank;

    [Header("Route (world; y is found on the ground)")]
    [Tooltip("The marked Runner spots on the main road (the user's R marks). It starts at the one nearest Khalid.")]
    [SerializeField] List<Vector3> spawns = new List<Vector3>();
    [Tooltip("Spots further down the road than this z go round the parked car and bins on this lane first.")]
    [SerializeField] float laneThresholdZ = -12f, laneX = -114.4f, laneEndZ = -13f;
    [Tooltip("From the main road up the side road, over the plank, to where it stands to shove it (last point).")]
    [SerializeField] List<Vector3> route = new List<Vector3>();
    [Tooltip("Off north after the shove (gone at the end).")]
    [SerializeField] List<Vector3> away = new List<Vector3>();
    [SerializeField] float runSpeed = 5.4f, escapeSpeed = 5.4f;
    [Tooltip("Ground speed its run clip matches at animator speed 1.")]
    [SerializeField] float runnerClipSpeed = 3.2f;

    [Header("Plank going down")]
    [Tooltip("Slides this far toward the near side (shoved from the far end) before its far end drops off the edge.")]
    [SerializeField] float slideDistance = 1.5f;
    [SerializeField] float slideSeconds = 0.55f, fallSeconds = 1.0f;
    [Tooltip("Where it ends up: wedged diagonally in the ravine, far end at the bottom.")]
    [SerializeField] Vector3 fallenPos = new Vector3(-86.6f, -1.5f, -8.3f);
    [SerializeField] float fallenTilt = 52f, fallenYaw = 7f;

    [Header("Camera")]
    [SerializeField] float noticeSeconds = 2.4f, followBlend = 2.2f;
    [Tooltip("Back to Khalid: a quick dip through black (a blend would fly the camera back through the buildings).")]
    [SerializeField] float backFadeOut = 0.4f, backFadeIn = 0.6f;
    [SerializeField] float followBack = 5.5f, followUp = 2.3f, followFov = 50f;
    [Tooltip("The trailing camera never goes past the near edge of the ravine (x).")]
    [SerializeField] float cameraMaxX = -92f;
    [Tooltip("Thin the Enviro height fog while it plays (to this density). OFF: the user didn't want the storm to clear when the " +
             "camera goes to the Runner - the trailing camera is close enough to see it in the normal fog.")]
    [SerializeField] bool thinFog = false;
    [SerializeField] float fogDensity = 0.3f;

    [Header("Sound")]
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField] AudioClip runnerScreech, plankCreak, plankDrop;

    // ---- runner beats after it reaches the shove spot (seconds)
    const float TurnTime = 0.35f, ShoveHit = 0.3f, ShoveEnd = 1.0f, StareScreech = 0.9f, StareEnd = 1.7f, BackAfterLeaving = 1.3f;
    const float RunAfter = 0.8f;    // stands looking at Khalid this long first

    enum Phase { Waiting, Playing, Done }
    enum Beat { Standing, Running, Turning, Shoving, Staring, Leaving, Gone }
    Phase phase = Phase.Waiting;
    Beat beat;
    float t, beatT, skipHeld;
    bool skipping, following, returning;


    readonly List<Vector3> pts = new List<Vector3>();
    readonly List<float> cum = new List<float>();
    float runnerS, runnerY, shoveS;

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
    Vector3 noticeOffset, followVel;
    float noticeStart;

    // khalid / adel / sound / fog
    readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();
    bool pausedMovement, madeInvincible;
    Transform adelHips, runnerShoulder;
    float adelHipHeight = 0.6f;
    AudioSource sfx;
    ScriptedFootsteps steps;
    float fogBase = -1f, fogW, fogTarget;

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

        var sgo = new GameObject("Bridge Runner Audio");
        sgo.transform.SetParent(transform, false);
        sfx = sgo.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 0.35f; sfx.dopplerLevel = 0f;
        sfx.rolloffMode = AudioRolloffMode.Linear; sfx.minDistance = 5f; sfx.maxDistance = 90f;
        sfx.outputAudioMixerGroup = sfxGroup;

        noticeCam = NewCam("Notice the Runner (from Khalid)", 50f);
        followCam = NewCam("Follow the Runner", followFov);
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

    /// <summary>Not playing: the plank where the story has it (a checkpoint / save restore can change that at any time).</summary>
    void UpdateIdleState()
    {
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
        if (phase == Phase.Playing) { UpdatePlaying(Time.deltaTime); return; }
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

    // ------------------------------------------------------------------ the sequence

    void Begin(bool debugForce)
    {
        var player = Player.instance;
        if (player == null || phase == Phase.Playing) return;
        if (AlreadyDone && !debugForce) return;
        phase = Phase.Playing; IsPlaying = true;
        t = 0f; beatT = 0f; skipHeld = 0f; skipping = following = returning = false; dropPlayed = false;
        StoryFlags.Set(DoneFlag);
        SetPlank(PlankState.OnBridge);

        // Khalid stops where he is and looks
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

        // the Runner at the marked spot nearest to where he came in
        Vector3 k = player.transform.position;
        Vector3 spawn = spawns.Count > 0 ? spawns[0] : transform.position;
        foreach (var s in spawns) if (Flat(s - k).sqrMagnitude < Flat(spawn - k).sqrMagnitude) spawn = s;
        BuildPath(Ground(spawn));
        if (runner != null)
        {
            runner.Appear(pts[0], k);
            runner.Idle();
            runnerS = 0f; runnerY = pts[0].y;
        }
        beat = Beat.Standing;

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
        var ra = RunnerAnimator;
        if (ra != null) steps.Track(ra, 0.95f);

        // camera: ease over from the gameplay view onto it, lens closing
        var cam = Camera.main;
        brain = cam != null ? cam.GetComponent<CinemachineBrain>() : null;
        if (brain != null)
        {
            savedBlend = brain.m_DefaultBlend;
            brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.8f);
        }
        noticeOffset = NoticeCameraOffset(player);
        noticeStart = Time.time;
        UpdateNoticeCam(true);
        noticeCam.Priority = 75;
        followVel = Vector3.zero;
        UpdateFollowCam(true);

        if (thinFog && EnviroSkyLite.instance != null && fogBase < 0f) fogBase = EnviroSkyLite.instance.fogSettings.heightDensity;
        fogTarget = thinFog ? 1f : 0f;
    }

    void Disable(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false; disabledBehaviours.Add(b);
    }

    Animator RunnerAnimator => runner != null ? runner.GetComponentInChildren<Animator>() : null;

    void UpdatePlaying(float dt)
    {
        if (skipping) return;
        t += dt; beatT += dt;
        if (t >= 0.3f && t - dt < 0.3f) PlayAt(runnerScreech, runner != null ? runner.transform.position : transform.position, 0.7f);
        UpdateRunner(dt);
        UpdatePlank(dt);

        if (!following && t >= noticeSeconds)
        {
            following = true;
            if (brain != null) brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, followBlend);
            followCam.Priority = 80;
        }
        if (!returning && (beat == Beat.Leaving || beat == Beat.Gone) && beatT >= BackAfterLeaving)
        {
            returning = true;
            StartCoroutine(Co_Back(backFadeOut));
            return;
        }
        if (!returning) HandleSkip();
    }

    /// <summary>Dip to black, Khalid's own camera again underneath, fade back in (the skip uses it too).</summary>
    IEnumerator Co_Back(float fadeOut)
    {
        skipping = true;            // stops the timeline where it is
        if (FadeScreenUI.instance != null) FadeScreenUI.instance.FadeOut(fadeOut);
        yield return new WaitForSecondsRealtime(fadeOut + 0.05f);
        Finish();
        if (FadeScreenUI.instance != null) FadeScreenUI.instance.FadeIn(backFadeIn);
    }

    void SetBeat(Beat b) { beat = b; beatT = 0f; }

    void UpdateRunner(float dt)
    {
        if (runner == null || beat == Beat.Gone) return;
        var player = Player.instance;
        switch (beat)
        {
            case Beat.Standing:
                if (player != null) runner.FaceDirection(player.transform.position - runner.transform.position, dt);
                if (t >= RunAfter) { runner.Run(); SetBeat(Beat.Running); }
                break;
            case Beat.Running:
                runnerS = Mathf.Min(shoveS, runnerS + runSpeed * dt);
                PlaceRunner(runSpeed, dt);
                if (runnerS >= shoveS - 0.001f) { runner.Idle(); SetBeat(Beat.Turning); }
                break;
            case Beat.Turning:
                // round to face the plank (back the way it came)
                runner.FaceDirection(-PathDir(shoveS - 0.5f), dt * 2.2f);
                if (beatT >= TurnTime)
                {
                    var ra = RunnerAnimator;
                    if (ra != null) { ra.speed = 1.3f; ra.CrossFadeInFixedTime("Attack", 0.12f, 0, 0.25f); }
                    SetBeat(Beat.Shoving);
                }
                break;
            case Beat.Shoving:
                runner.FaceDirection(-PathDir(shoveS - 0.5f), dt);
                if (plankT < 0f && beatT >= ShoveHit) StartPlankFall();
                if (beatT >= ShoveEnd)
                {
                    var ra = RunnerAnimator;
                    if (ra != null) ra.CrossFadeInFixedTime("Movement", 0.2f);
                    runner.Idle();
                    SetBeat(Beat.Staring);
                }
                break;
            case Beat.Staring:
                // it looks back across at Khalid's side, Adel over its shoulder
                runner.FaceDirection(-PathDir(shoveS - 0.5f), dt);
                if (beatT >= StareScreech && beatT - dt < StareScreech) PlayAt(runnerScreech, runner.transform.position, 0.8f);
                if (beatT >= StareEnd) { runner.Run(); SetBeat(Beat.Leaving); }
                break;
            case Beat.Leaving:
                runnerS = Mathf.Min(PathLength, runnerS + escapeSpeed * dt);
                PlaceRunner(escapeSpeed, dt);
                if (runnerS >= PathLength - 0.05f)
                {
                    runner.Vanish();
                    if (adel != null) adel.gameObject.SetActive(false);
                    if (steps != null) steps.End();
                    beat = Beat.Gone;     // keep beatT running (the camera goes back a moment after it left)
                }
                break;
        }
    }

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

    void Finish()
    {
        var player = Player.instance;
        // where everything ends up (a skip jumps here)
        SetPlank(PlankState.Fallen);
        if (runner != null) runner.Vanish();
        if (adel != null) adel.gameObject.SetActive(false);
        if (steps != null) steps.End();
        beat = Beat.Gone;
        if (noticeCam != null) noticeCam.Priority = 0;
        if (followCam != null) followCam.Priority = 0;
        // under black: straight back to his camera
        if (brain != null) brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);
        fogTarget = 0f;
        ReleasePlayer(player);
        if (CutsceneSkipper.Instance != null) CutsceneSkipper.Instance.HideHint();
        phase = Phase.Done; IsPlaying = false;
        StartCoroutine(Co_After(skipping));
        skipping = false;
    }

    void ReleasePlayer(Player player)
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

    IEnumerator Co_After(bool skipped)
    {
        yield return null;
        if (brain != null) brain.m_DefaultBlend = savedBlend;
        // he's himself again: dying later, Continue or a reload come back here with the bridge gone, never before it
        yield return null; yield return null;
        if (CheckpointManager.instance != null) CheckpointManager.instance.TriggerCheckpoint(CheckpointName);
    }

    void LateUpdate()
    {
        if (phase == Phase.Playing && !skipping)
        {
            UpdateNoticeCam(false);
            UpdateFollowCam(false);
            PoseAdelCarried();
            // Khalid keeps looking where it is
            var player = Player.instance;
            if (player != null && runner != null && runner.gameObject.activeSelf)
            {
                Vector3 d = Flat(runner.transform.position - player.transform.position);
                if (d.sqrMagnitude > 0.01f)
                    player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, Quaternion.LookRotation(d), 220f * Time.deltaTime);
            }
        }
        // Enviro eases the fog back to its weather preset every frame: ours goes on top while it lasts
        if (fogBase >= 0f && EnviroSkyLite.instance != null)
        {
            fogW = Mathf.MoveTowards(fogW, fogTarget, Time.deltaTime / (fogTarget > fogW ? 1.0f : 1.6f));
            EnviroSkyLite.instance.fogSettings.heightDensity = Mathf.Lerp(fogBase, Mathf.Min(fogBase, fogDensity), Mathf.SmoothStep(0f, 1f, fogW));
            if (fogW <= 0f && fogTarget <= 0f) fogBase = -1f;
        }
    }

    // ------------------------------------------------------------------ cameras

    Vector3 RunnerChest => runner.transform.position + Vector3.up * 1.3f;

    /// <summary>From beside Khalid onto the Runner, the lens closing to ~5 m of frame at its distance.</summary>
    void UpdateNoticeCam(bool snap)
    {
        var player = Player.instance;
        if (noticeCam == null || player == null || runner == null) return;
        Vector3 pos = player.transform.position + noticeOffset;
        Vector3 target = RunnerChest;
        Quaternion rot = Quaternion.LookRotation(target - pos, Vector3.up);
        noticeCam.transform.SetPositionAndRotation(pos, snap ? rot : Quaternion.Slerp(noticeCam.transform.rotation, rot, 1f - Mathf.Exp(-8f * Time.deltaTime)));
        float dist = Vector3.Distance(pos, target);
        float tight = Mathf.Clamp(2f * Mathf.Atan(2.6f / Mathf.Max(1f, dist)) * Mathf.Rad2Deg, 8f, 40f);
        float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(noticeStart + 0.2f, noticeStart + 1.3f, Time.time));
        noticeCam.m_Lens.FieldOfView = Mathf.Lerp(50f, tight, u);
    }

    /// <summary>Trails the Runner along its route (behind and above, by the route's direction so it never swings round when the
    /// Runner turns), and stops at the near edge of the ravine to watch it shove the plank in and go.</summary>
    void UpdateFollowCam(bool snap)
    {
        if (followCam == null || runner == null || pts.Count < 2) return;
        float s = Mathf.Min(runnerS, shoveS);
        Vector3 dir = beat >= Beat.Turning ? PathDirAtShove() : PathDir(s);
        Vector3 at = PathPoint(s, out _);
        Vector3 want = at - dir * followBack;
        if (want.x > cameraMaxX) want.x = cameraMaxX;
        want = Ground(new Vector3(want.x, at.y, want.z)) + Vector3.up * followUp;
        // something in the way (a car, a wall): rise over it
        Vector3 chest = RunnerChest;
        for (float lift = 0f; lift <= 4.5f; lift += 1.5f)
            if (!Blocked(want + Vector3.up * lift, chest)) { want += Vector3.up * lift; break; }
        Vector3 pos = snap ? want : Vector3.SmoothDamp(followCam.transform.position, want, ref followVel, 0.45f);
        // look at the Runner (a little ahead of it while it runs); while the plank goes, take it in too
        Vector3 look = chest + (beat == Beat.Running ? dir * 1.5f : Vector3.zero);
        if (plank != null && (plankState == PlankState.Falling || (plankState == PlankState.Fallen && beat == Beat.Staring && beatT < 0.8f)))
            look = Vector3.Lerp(look, plank.position, 0.4f);
        Quaternion rot = Quaternion.LookRotation(look - pos, Vector3.up);
        followCam.transform.SetPositionAndRotation(pos, snap ? rot : Quaternion.Slerp(followCam.transform.rotation, rot, 1f - Mathf.Exp(-6f * Time.deltaTime)));
        followCam.m_Lens.FieldOfView = followFov;
    }

    static bool Blocked(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        foreach (var h in Physics.RaycastAll(a, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (Player.instance != null && h.collider.transform.IsChildOf(Player.instance.transform)) continue;
            if (IsInvisible(h.collider)) continue;
            return true;
        }
        return false;
    }

    static bool IsInvisible(Collider c)
    {
        // invisible blockers (Boundaries cubes) have their renderer switched off
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
        // well off to the side: his head stays out of the frame once the lens closes in
        foreach (float lift in new[] { 0.45f, 1.4f, 2.5f, 4f, 6f })
        {
            Vector3 p = head - to * 1.8f + right * 1.0f + Vector3.up * lift;
            if (!Blocked(head, p) && !Blocked(p, target)) return p - k;
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

    void BuildPath(Vector3 start)
    {
        pts.Clear(); cum.Clear();
        pts.Add(start);
        // down the road: round the parked car and the bins on the far lane first
        if (start.z < laneThresholdZ)
        {
            pts.Add(Ground(new Vector3(laneX, start.y, Mathf.Min(start.z + 2.5f, laneEndZ))));
            pts.Add(Ground(new Vector3(laneX, start.y, laneEndZ)));
        }
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

    // ------------------------------------------------------------------ sound

    void PlayAt(AudioClip clip, Vector3 pos, float vol)
    {
        if (clip == null || sfx == null) return;
        sfx.transform.position = pos;
        sfx.PlayOneShot(clip, vol);
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
        returning = true;
        yield return Co_Back(0.35f);
    }

    void OnDisable()
    {
        if (phase == Phase.Playing)
        {
            // going away mid-way (scene unload / stop): no coroutines, just give him back
            ReleasePlayer(Player.instance);
            if (brain != null) brain.m_DefaultBlend = savedBlend;
            phase = Phase.Done;
        }
        IsPlaying = false;
        if (fogBase >= 0f && EnviroSkyLite.instance != null) EnviroSkyLite.instance.fogSettings.heightDensity = fogBase;
        fogBase = -1f; fogW = 0f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.DrawCube(areaCenter, areaSize);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(areaCenter, areaSize);
        Gizmos.color = Color.green;
        foreach (var s in spawns) Gizmos.DrawSphere(s, 0.6f);
        Gizmos.color = Color.yellow;
        for (int i = 1; i < route.Count; i++) Gizmos.DrawLine(route[i - 1], route[i]);
    }

    // ------------------------------------------------------------------ debug

    /// <summary>Debug: Khalid just inside the area level with spawn 'index' (Play mode), and it plays.</summary>
    public void DebugPlayNow(int index)
    {
        var player = Player.instance;
        if (!Application.isPlaying || player == null || phase == Phase.Playing || spawns.Count == 0) return;
        Vector3 s = spawns[Mathf.Clamp(index, 0, spawns.Count - 1)];
        Vector3 at = Ground(new Vector3(areaCenter.x - areaSize.x * 0.5f + 1.5f, s.y, s.z));
        if (player.controller != null) player.controller.enabled = false;
        player.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 90f, 0f));
        if (player.controller != null) player.controller.enabled = true;
        player.OnPlayerTelported?.Invoke();
        plankState = PlankState.Unset;
        phase = Phase.Waiting;
        Begin(true);
    }

    public void DebugSkip() { if (phase == Phase.Playing && !skipping) StartCoroutine(Co_Skip()); }
    public string DebugState => phase + " " + beat + " t " + t.ToString("F1") + " s " + runnerS.ToString("F1") + "/" + shoveS.ToString("F1") + " plank " + plankState;
    public int SpawnCount => spawns.Count;
}
