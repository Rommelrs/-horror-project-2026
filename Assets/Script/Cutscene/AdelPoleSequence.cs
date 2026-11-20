using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using UnityEngine.Localization;

/// <summary>
/// The middle street between Blocks 2 and 3 (user 2026-11-18). After the Runner carried Adel off over the bridge, Adel is
/// tied to the light pole on the corner of that street, and the knife Runner (Between Alley 2-3/RunnerEnemy (1)) is there.
///
/// 1. Khalid walks into the street (area box, no collider) -> short cutscene, no dialogue: he sees Adel tied to the pole,
///    the Runner far down the street turns and charges (user: "runs from afar, dont make him close"), Khalid raises the gun.
///    Control comes back AIMING (forced ADS, user) until he presses the aim button himself, then everything is normal.
/// 2. The fight is the normal game (the Runner's own AI).
/// 3. Once it is dead: Khalid picks up the knife blade it dropped, runs to the pole and cuts Adel free; Adel shoves him -
///    "All of you are trying to murder me!!" - runs to Block 3's side gate and crawls under it (a hollow dug under the right
///    leaf: the gate itself only leaves 11 cm), then Khalid: "Adel wait!". Gameplay resumes; checkpoint.
///
/// Adel has only idle / walk / run clips, so being tied, the shove and the crawl are poses laid over them here (two-bone
/// IK on his arms and legs, the body laid flat for the crawl). Built by Tools > Cutscenes > Build Adel Pole Sequence.
/// </summary>
[DefaultExecutionOrder(150)]
public class AdelPoleSequence : MonoBehaviour
{
    public const string DoneFlag = "AdelPoleDone";
    public static bool IsPlaying { get; private set; }

    [Header("When")]
    [Tooltip("Adel is only tied here once the bridge Runner has carried him off.")]
    [SerializeField] string requiredFlag = BridgeRunnerSequence.DoneFlag;
    [Tooltip("The middle street (world box, no collider): Khalid inside it -> it starts.")]
    [SerializeField] Vector3 areaCenter = new Vector3(-55f, 3f, -10f);
    [SerializeField] Vector3 areaSize = new Vector3(50f, 16f, 28f);
    [SerializeField] string checkpointName = "Adel ran into Block 3";

    [Header("Actors")]
    [SerializeField] Enemy runner;
    [Tooltip("Where the Runner waits for the cutscene: the far one of these from Khalid (the street's two ends).")]
    [SerializeField] List<Vector3> runnerFarSpots = new List<Vector3>();
    [SerializeField] Animator adel;
    [Tooltip("The light pole (its shaft's centre at the ground).")]
    [SerializeField] Transform pole;
    [SerializeField] float poleRadius = 0.1f;
    [SerializeField] GameObject ropeTied, ropeCut;
    [SerializeField] Item knifeBlade;

    [Header("Where things happen (world)")]
    [Tooltip("Where Khalid stands to cut him loose (beside him, facing the pole).")]
    [SerializeField] Vector3 khalidFreeSpot;
    [Tooltip("Adel's way to the gate: last point = where he gets down to crawl.")]
    [SerializeField] List<Vector3> adelPath = new List<Vector3>();
    [Tooltip("Crawling: his feet go from the last path point to here (through the hollow under the gate).")]
    [SerializeField] Vector3 crawlTo;
    [Tooltip("After the gate, off into Block 3 (he's gone at the end).")]
    [SerializeField] List<Vector3> afterGate = new List<Vector3>();
    [SerializeField] Transform gateShot;

    [Header("Lines")]
    [SerializeField] string adelLine = "All of you are trying to murder me!!";
    [SerializeField] string khalidLine = "Adel wait!";

    [Header("Sound")]
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField] AudioClip ropeCutClip, pickupClip, shoveClip;

    // ---- tuning
    const float AdelRunSpeed = 3.6f, AdelClipSpeed = 3.6f, CrawlSpeed = 0.7f;
    const int IntroRounds = 3;          // rounds in the gun when the intro starts (topped up, never taken away)
    const float KhalidRun = 2.9f, KhalidWalk = 1.25f;
    [Tooltip("The Runner's speed while it barges in from the far end (its normal chase is 2): back to normal once it's close.")]
    [SerializeField] float bargeSpeed = 3.6f;

    enum Phase { Waiting, Intro, Fight, Outro, Done }
    Phase phase = Phase.Waiting;
    bool debugArmed, forceAim, runnerDied, barging, tpAim, bladeSecured, runnerHeld;
    // the street's old aggro trigger (Between Alley 2-3 Gameplay/EnemyAggroTrigger) woke the Runner before Khalid reached the
    // area, so the fight started with no cutscene: while Adel waits at the pole it is off,
    // and the Runner sleeps - idle, its AI paused - until the cutscene wakes it
    readonly List<Collider> aggroBoxes = new List<Collider>();
    bool runnerDormant, wokenEarly;
    Transform camFollow; Vector3 followOffset, followLook;
    float savedRunnerSpeed = -1f;
    float runnerDiedAt;

    // khalid
    readonly List<Behaviour> disabled = new List<Behaviour>();
    bool pausedMovement, madeInvincible, savedWeapon, gunHidden;
    PlayerWeaponEnabler gunEnabler;
    bool scriptedMove; Vector3 moveTarget; float moveSpeed; Vector3 lookAt; bool lookAtSet;

    // cameras
    CinemachineVirtualCamera camA, camB;
    CinemachineVirtualCamera live;
    CinemachineBrain brain;
    CinemachineBlendDefinition savedBlend; bool blendSaved;

    // adel
    Transform hips, spine, lUp, lLo, lHand, rUp, rLo, rHand, lThigh, lShin, lFoot, rThigh, rShin, rFoot;
    float armA, armB, legA, legB;
    float tiedW = 1f, pushW, crawlW; float crawlPhase;
    Vector3 crawlDir = Vector3.back;
    AudioSource sfx;
    ScriptedFootsteps adelSteps;

    // ------------------------------------------------------------------ setup

    void Awake()
    {
        if (adel != null)
        {
            tiedPos = adel.transform.position; tiedRot = adel.transform.rotation; tiedKnown = true;
            adel.applyRootMotion = false;
            adel.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var t in adel.GetComponentsInChildren<Transform>(true))
                switch (t.name)
                {
                    case "Hips": hips = t; break;
                    case "Spine": spine = t; break;
                    case "LeftUpperArm": lUp = t; break;
                    case "LeftLowerArm": lLo = t; break;
                    case "LeftLowerArm.001": lHand = t; break;
                    case "RightUpperArm": rUp = t; break;
                    case "RightLowerArm": rLo = t; break;
                    case "RightLowerArm.001": rHand = t; break;
                    case "LeftUpperLeg": lThigh = t; break;
                    case "LeftLowerLeg": lShin = t; break;
                    case "LeftFoot": lFoot = t; break;
                    case "RightUpperLeg": rThigh = t; break;
                    case "RightLowerLeg": rShin = t; break;
                    case "RightFoot": rFoot = t; break;
                }
        }
        var go = new GameObject("Adel Pole Audio");
        go.transform.SetParent(transform, false);
        sfx = go.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 0.4f; sfx.outputAudioMixerGroup = sfxGroup; sfx.rolloffMode = AudioRolloffMode.Linear;
        sfx.minDistance = 4f; sfx.maxDistance = 40f;
        camA = NewCam("Adel pole cam A");
        camB = NewCam("Adel pole cam B");
    }

    CinemachineVirtualCamera NewCam(string n)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var c = go.AddComponent<CinemachineVirtualCamera>();
        c.m_Lens.FieldOfView = 50f; c.m_Lens.NearClipPlane = 0.08f; c.m_Lens.FarClipPlane = 60f;
        c.Priority = 0;
        return c;
    }

    void Start()
    {
        if (runner != null)
        {
            runner.OnEnemyDied.AddListener(OnRunnerDied);
            var hp = runner.GetComponent<Health>();
            if (hp != null) hp.OnDamageTaken += OnRunnerHurt;
            foreach (var t in FindObjectsByType<EnemyAggroTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.Wakes(runner))
                    aggroBoxes.AddRange(t.GetComponents<Collider>());
        }
        IdleState();
    }

    void OnDestroy()
    {
        if (runner == null) return;
        runner.OnEnemyDied.RemoveListener(OnRunnerDied);
        var hp = runner.GetComponent<Health>();
        if (hp != null) hp.OnDamageTaken -= OnRunnerHurt;
    }

    // shot while it slept (from far off, before the cutscene): no cutscene then, it simply comes for him - a normal fight,
    // and the outro still plays once it is dead
    void OnRunnerHurt(int dmg)
    {
        if (!runnerDormant || phase != Phase.Waiting) return;
        SetRunnerDormant(false);
        wokenEarly = true;
        runner.ChangeEnemyType(EnemyType.Aggressive);
        phase = Phase.Fight;
    }

    /// <summary>While Adel waits at the pole: the aggro trigger off, the Runner asleep. Off again when the cutscene starts.</summary>
    void KeepRunnerAsleep(bool asleep)
    {
        foreach (var c in aggroBoxes) if (c != null && c.enabled == asleep) c.enabled = !asleep;
        if (asleep && !RunnerDead)
        {
            if (runner.stats != null && runner.stats.enemyType != EnemyType.Fixed) runner.ChangeEnemyType(EnemyType.Fixed);   // a save's trigger re-wake
            if (!runnerDormant) SetRunnerDormant(true);
        }
        else if (runnerDormant) SetRunnerDormant(false);
    }

    void SetRunnerDormant(bool on)
    {
        runnerDormant = on;
        if (runner == null) return;
        runner.PauseEnemyState(on);
        if (runner.agent != null && runner.agent.isOnNavMesh) runner.agent.isStopped = on;
    }

    // the road through the crossroads (the area box), or right up to the pole by any other way
    bool InStartArea(Vector3 pos)
    {
        if (new Bounds(areaCenter, areaSize).Contains(pos)) return true;
        return pole != null && Flat(pos - pole.position).magnitude < 11f && Mathf.Abs(pos.y - pole.position.y) < 4f;
    }

    void OnRunnerDied() { runnerDied = true; runnerDiedAt = Time.time; }

    bool RunnerDead => runner == null || runnerDied || !runner.gameObject.activeInHierarchy
                       || (runner.GetComponent<Health>() != null && runner.GetComponent<Health>().IsDead);
    bool Armed => debugArmed || (!StoryFlags.Has(DoneFlag) && (string.IsNullOrEmpty(requiredFlag) || StoryFlags.Has(requiredFlag)));

    /// <summary>Not playing: Adel tied to the pole while the story has him there, else nothing.</summary>
    void IdleState()
    {
        bool tied = Armed && phase != Phase.Done;
        if (adel != null && adel.gameObject.activeSelf != tied)
        {
            adel.gameObject.SetActive(tied);
            if (tied) { PlaceAdelAtPole(); adel.Play("Idle", 0, Random.value); }
        }
        if (ropeTied != null && ropeTied.activeSelf != tied) ropeTied.SetActive(tied);
        if (ropeCut != null && ropeCut.activeSelf) ropeCut.SetActive(false);
        tiedW = 1f; pushW = 0f; crawlW = 0f;
    }

    void PlaceAdelAtPole()
    {
        if (adel == null || !tiedKnown) return;
        adel.transform.SetPositionAndRotation(AdelTiedPos, AdelTiedRot);
    }

    // where the builder stood him (back to the pole, facing the street)
    Vector3 tiedPos; Quaternion tiedRot = Quaternion.identity; bool tiedKnown;
    Vector3 PoleBase => Ground(pole.position);
    Vector3 AdelTiedPos => tiedPos;
    Quaternion AdelTiedRot => tiedRot;

    // ------------------------------------------------------------------ main loop

    void Update()
    {
        var p = Player.instance;
        if (runnerDied && !bladeSecured) SecureDroppedBlade();
        switch (phase)
        {
            case Phase.Waiting:
                IdleState();
                KeepRunnerAsleep(Armed);
                if (Armed && p != null && InStartArea(p.transform.position) && CanStart(p))
                {
                    if (RunnerDead) StartCoroutine(Co_Outro());     // killed from a distance before he walked in
                    else StartCoroutine(Co_Intro());
                }
                break;
            case Phase.Fight:
                UpdateForcedAim(p);
                if (barging && (RunnerDead || (p != null && Vector3.Distance(runner.transform.position, p.transform.position) < 5f))) EndBarge();
                if (RunnerDead && Time.time > runnerDiedAt + 1.8f && p != null && CanStart(p) && !p.IsDead()) StartCoroutine(Co_Outro());
                break;
        }
        if (scriptedMove && p != null) MoveKhalid(p);
        if (tpAim && p != null && p.animator != null) p.animator.SetBool("Aiming", true);
    }

    /// <summary>The blade it drops has a loose Rigidbody that falls straight through the street's road mesh (it was lost in
    /// the ground even before this sequence): held where it landed the moment it appears.</summary>
    void SecureDroppedBlade()
    {
        if (runner == null || knifeBlade == null) { bladeSecured = true; return; }
        foreach (var pk in FindObjectsByType<InspectableItemPickup>(FindObjectsSortMode.None))
        {
            if (pk == null || pk.itemToPickup != knifeBlade || (pk.transform.position - runner.transform.position).sqrMagnitude > 16f) continue;
            foreach (var rb in pk.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true; rb.detectCollisions = false;
                rb.transform.localPosition = Vector3.zero;
            }
            pk.transform.position = Ground(pk.transform.position) + Vector3.up * 0.03f;
            LayFlat(pk);
            bladeSecured = true;
        }
        if (Time.time > runnerDiedAt + 3f) bladeSecured = true;
    }

    /// <summary>The blade model stands on its point when dropped: laid down on the ground, long side flat.</summary>
    void LayFlat(InspectableItemPickup pk)
    {
        foreach (var c in pk.GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) c.enabled = false;   // its own box, not the pickup sphere
        Renderer r = null;
        foreach (var rr in pk.GetComponentsInChildren<MeshRenderer>(true)) if (rr.GetComponent<MeshFilter>() != null) { r = rr; break; }
        if (r == null) return;
        // its thinnest side (the flat of the blade) facing up
        var mesh = r.GetComponent<MeshFilter>().sharedMesh;
        if (mesh != null)
        {
            Vector3 sz = Vector3.Scale(mesh.bounds.size, r.transform.lossyScale);
            Vector3 thin = sz.x <= sz.y && sz.x <= sz.z ? Vector3.right : sz.y <= sz.z ? Vector3.up : Vector3.forward;
            Vector3 thinWorld = r.transform.TransformDirection(thin);
            if (Vector3.Dot(thinWorld, Vector3.up) < 0f) thinWorld = -thinWorld;
            Quaternion q = Quaternion.FromToRotation(thinWorld, Vector3.up);
            q.ToAngleAxis(out float ang, out Vector3 axis);
            if (ang > 0.5f) r.transform.RotateAround(r.bounds.center, axis, ang);
        }
        Bounds b = r.bounds;
        float g = Ground(b.center).y;
        r.transform.position += Vector3.up * (g + 0.004f - b.min.y);
    }

    void EndBarge()
    {
        barging = false;
        if (runner != null && runner.stats != null && savedRunnerSpeed > 0f) runner.stats.movementSpeed = savedRunnerSpeed;
    }

    bool CanStart(Player p)
    {
        if (GameManager.IsPaused || CheckpointManager.IsLoadingCheckpoint) return false;
        if (p.pauseMovement || p.isGrabbed) return false;
        if (KidnapSequence.IsPlaying || BridgeRunnerSequence.IsPlaying) return false;
        if (ItemInspectionHandler.instance != null && ItemInspectionHandler.instance.InspectionMenuIsActive()) return false;
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) return false;
        return true;
    }

    // ------------------------------------------------------------------ 1. the cutscene in

    IEnumerator Co_Intro()
    {
        var player = Player.instance;
        phase = Phase.Intro; IsPlaying = true;
        KeepRunnerAsleep(false);
        HoldKhalid(player, false);
        bool hasGun = player.inventory != null && player.inventory.HasWeapon();
        if (hasGun && PlayerMeleeWeapon.Equipped) PlayerMeleeWeapon.SelectGun();      // the gun comes out while we look at Adel
        if (hasGun && player.playerWeaponSystem != null) player.playerWeaponSystem.weaponIsEnabled = true;
        // never an empty gun in the forced aim: at least 3 rounds in it (more stay if he has them)
        if (hasGun && player.playerWeaponSystem != null) player.playerWeaponSystem.EnsureLoaded(IntroRounds);

        // the Runner, idle at the far end of the street from him
        Vector3 k = player.transform.position;
        Vector3 far = runner.transform.position;
        // about 22 m down the street from him (not so far it's lost in the fog); the farthest spot if none is far enough
        float best = float.MaxValue, farthest = -1f; Vector3 farthestSpot = far; bool anyOk = false;
        foreach (var s in runnerFarSpots)
        {
            float d = Flat(s - k).magnitude;
            if (d > farthest) { farthest = d; farthestSpot = s; }
            if (d < 14f) continue;
            float score = Mathf.Abs(d - 22f);
            if (score < best) { best = score; far = s; anyOk = true; }
        }
        if (!anyOk) far = farthestSpot;
        runner.ChangeEnemyType(EnemyType.Fixed);
        if (runner.agent != null && runner.agent.isOnNavMesh) runner.agent.Warp(far); else runner.transform.position = Ground(far);
        runner.transform.rotation = Quaternion.LookRotation(Flat(k - far));

        BeginCameras();
        Vector3 adelChest = adel.transform.position + Vector3.up * 1.0f;

        // shot 1 (0 - 3.2): over his shoulder, he sees Adel tied to the pole
        lookAt = adelChest; lookAtSet = true;
        // over Khalid's shoulder only when he's close: past ~9 m the cutscene fog hides Adel, so then the camera is down the
        // street near the pole, on Khalid's side of it, looking at Adel tied there
        Vector3 toAdel = Flat(adelChest - k).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, toAdel);
        if (Flat(adelChest - k).magnitude < 9f)
            SetShot(camA, k - toAdel * 3.3f + side * 1.35f + Vector3.up * 1.8f, adelChest, 26f, 55f);
        else
            SetShot(camA, adelChest - toAdel * 5.2f + side * 1.3f + Vector3.up * 0.55f, adelChest + Vector3.up * 0.1f, 34f, 55f);
        yield return Wait(3.2f);

        // shot 2 (3.2 - 6.6): on the Runner itself, down the street - standing there with the knife, facing him
        Vector3 rPos = runner.transform.position;
        Vector3 rToK = Flat(k - rPos).normalized;
        Vector3 rSide = Vector3.Cross(Vector3.up, rToK);
        lookAt = rPos + Vector3.up * 1.4f;
        SetShot(camB, rPos + rToK * 3.1f + rSide * 1.5f + Vector3.up * 1.05f, rPos + Vector3.up * 1.15f, 36f, 40f);
        yield return Wait(1.6f);
        // a slow push in on it (the knife hand) before it moves
        for (float e = 0f; e < 1.8f; e += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, e / 1.8f);
            camB.transform.position = rPos + rToK * Mathf.Lerp(3.1f, 2.3f, u) + rSide * Mathf.Lerp(1.5f, 1.1f, u) + Vector3.up * 1.05f;
            camB.transform.rotation = Quaternion.LookRotation(rPos + Vector3.up * 1.15f - camB.transform.position);
            yield return null;
        }

        // shot 3 (6.6 - 8.8): behind Khalid, long lens - it comes for him out of the fog
        k = player.transform.position;
        Vector3 rChest = runner.transform.position + Vector3.up * 1.4f;
        Vector3 toR = Flat(rChest - k).normalized;
        side = Vector3.Cross(Vector3.up, toR);
        lookAt = rChest;
        SetShot(camA, k - toR * 3.4f - side * 1.45f + Vector3.up * 1.6f, rChest, 28f, 60f);
        runner.chaseStartDelayEnabled = false;
        if (runner.stats != null) { if (savedRunnerSpeed < 0f) savedRunnerSpeed = runner.stats.movementSpeed; runner.stats.movementSpeed = bargeSpeed; barging = true; }
        runner.ChangeEnemyType(EnemyType.Aggressive);
        yield return Wait(2.2f);

        // shot 4 (8.8 - 10.4): running alongside it, low, the knife swinging
        camFollow = runner.transform; followOffset = new Vector3(1.4f, 1.0f, 3.0f); followLook = new Vector3(0f, 1.1f, 0f);
        SetShot(camB, FollowPos(), runner.transform.position + Vector3.up * 1.1f, 42f, 40f);
        yield return Wait(1.6f);
        camFollow = null;
        // off camera while he raises the gun: it holds back, so it is still a way off when he has control
        if (runner.agent != null && runner.agent.isOnNavMesh) runner.agent.isStopped = true;
        runner.PauseEnemyState(true);
        runnerHeld = true;

        // shot 5 (10.4 - 12.4): in front of him, he raises the gun at it
        k = player.transform.position;
        rChest = runner.transform.position + Vector3.up * 1.4f;
        toR = Flat(rChest - k).normalized;
        side = Vector3.Cross(Vector3.up, toR);
        lookAt = rChest;
        player.transform.rotation = Quaternion.LookRotation(toR);
        tpAim = hasGun;          // his own (third person) aim pose for the shot; the first person ADS comes with control
        SetShot(camA, k + toR * 2.6f + side * 1.3f + Vector3.up * 1.5f, k + Vector3.up * 1.4f, 40f, 30f);
        yield return Wait(2.0f);
        tpAim = false;
        if (runner.agent != null && runner.agent.isOnNavMesh) runner.agent.isStopped = false;
        runner.PauseEnemyState(false);
        runnerHeld = false;
        if (hasGun) { forceAim = true; SetForcedAim(player, true); }

        // control back, still aiming (until he presses aim himself)
        k = player.transform.position;
        player.transform.rotation = Quaternion.LookRotation(Flat(runner.transform.position - k));
        if (player.playerWeaponSystem != null) player.playerWeaponSystem.SyncCameraYaw();
        lookAtSet = false;
        EndCameras(0.5f);
        ReleaseKhalid(player);
        phase = Phase.Fight;
        IsPlaying = false;
    }

    // forced ADS: the gun stays up until he presses the aim button himself, then it's his again
    void UpdateForcedAim(Player p)
    {
        if (!forceAim || p == null) return;
        var ws = p.playerWeaponSystem;
        if (ws == null || p.IsDead()) { forceAim = false; return; }
        if (ws.AimButtonHeld || PlayerMeleeWeapon.Equipped || (p.inventory != null && !p.inventory.HasWeapon()))
        {
            forceAim = false;
            SetForcedAim(p, false);
            return;
        }
        ws.aimingOverrideForTesting = true;
    }

    void SetForcedAim(Player p, bool on)
    {
        if (p == null || p.playerWeaponSystem == null) return;
        p.playerWeaponSystem.aimingOverrideForTesting = on;
    }

    // ------------------------------------------------------------------ 3. after the fight

    IEnumerator Co_Outro()
    {
        var player = Player.instance;
        phase = Phase.Outro; IsPlaying = true;
        forceAim = false; SetForcedAim(player, false);
        if (player.playerWeaponSystem != null) player.playerWeaponSystem.ExitOutOfAiming();
        HoldKhalid(player, true);
        HideGun(player);
        BeginCameras();

        // the blade it dropped (EnemyItemDrop spawns KnifeBladePickup where it died)
        InspectableItemPickup blade = null;
        Vector3 deadAt = runner != null ? runner.transform.position : player.transform.position;
        float bestD = 9f * 9f;
        foreach (var pk in FindObjectsByType<InspectableItemPickup>(FindObjectsSortMode.None))
        {
            if (pk == null || pk.itemToPickup != knifeBlade) continue;
            float d = (pk.transform.position - deadAt).sqrMagnitude;
            if (d < bestD) { bestD = d; blade = pk; }
        }

        if (blade != null)
        {
            // it lands under the body: knocked a step toward Khalid ("it dropped the knife") so it can be seen and reached
            Vector3 awayFromBody = Flat(player.transform.position - deadAt);
            if (awayFromBody.sqrMagnitude < 0.01f) awayFromBody = Flat(khalidFreeSpot - deadAt);
            foreach (var rb in blade.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.detectCollisions = false; }   // or it sinks through the road
            blade.transform.position = Ground(deadAt + awayFromBody.normalized * 1.1f) + Vector3.up * 0.03f;
            LayFlat(blade);
            Vector3 b = Ground(blade.transform.position);
            Vector3 fromPole = Flat(player.transform.position - b);
            if (fromPole.sqrMagnitude < 0.01f) fromPole = Flat(khalidFreeSpot - b);
            Vector3 stand = b + fromPole.normalized * 0.65f;
            // far from it (the fight moved off): under a quick dip to black he's a few steps away
            if (Flat(player.transform.position - stand).magnitude > 6f)
            {
                yield return Fade(true, 0.35f);
                TeleportKhalid(player, stand + fromPole.normalized * 3.2f, Quaternion.LookRotation(-fromPole));
                yield return null;
                StartCoroutine(Fade(false, 0.45f));
            }
            // shot: from the side, he walks up to the blade
            Vector3 dir = Flat(stand - player.transform.position).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = -fromPole.normalized;
            Vector3 sideV = Vector3.Cross(Vector3.up, dir);
            Vector3 mid = (player.transform.position + stand) * 0.5f;
            SetShot(camA, mid + sideV * 3.6f + Vector3.up * 1.6f, mid + Vector3.up * 0.9f, 48f, 30f);
            yield return WalkKhalidTo(stand, KhalidWalk, 4f, b);
            // shot: low, on the blade on the ground - then it's in his hand (inventory)
            Vector3 lowCam = b + sideV * 0.75f - dir * 0.35f + Vector3.up * 0.38f;
            SetShot(camB, lowCam, b + Vector3.up * 0.06f, 44f, 30f);
            yield return Wait(1.5f);
            TakeBlade(blade);
            HideGun(player);           // the pickup's inventory update puts the gun back in his hand
            yield return Wait(1.1f);
        }
        else if (knifeBlade != null && player.inventory != null && !OwnsBlade(player))
        {
            // Continue from the checkpoint its death made ("Knife blade pickup"): the Runner is restored dead, so it never
            // drops the blade again and the outro had nothing to pick up - no knife, no way through the Block 3 cloth.
            player.inventory.PickUp(knifeBlade, 1);
            HideGun(player);
        }

        // he runs to the pole (a cut hides the long part)
        Vector3 free = Ground(khalidFreeSpot);
        Vector3 runDir = Flat(free - player.transform.position);
        if (runDir.magnitude > 7.5f)
        {
            Vector3 start = free - runDir.normalized * 7f;
            TeleportKhalid(player, Ground(start), Quaternion.LookRotation(runDir));
        }
        Vector3 adelChest = adel.transform.position + Vector3.up * 0.95f;
        Vector3 inFront = AdelTiedRot * Vector3.forward;
        Vector3 aSide = AdelTiedRot * Vector3.right;
        SetShot(camA, adel.transform.position + inFront * 2.6f - aSide * 1.4f + Vector3.up * 1.35f, (adelChest + free + Vector3.up * 1.2f) * 0.5f, 50f, 30f);
        yield return WalkKhalidTo(free, KhalidRun, 4f, PoleBase + Vector3.up * 1f);
        lookAt = PoleBase + Vector3.up * 1f; lookAtSet = true;

        // cut loose: a close two-shot
        SetShot(camB, adel.transform.position + inFront * 1.55f + aSide * 0.55f + Vector3.up * 1.15f, (adelChest + free + Vector3.up * 1.25f) * 0.5f, 46f, 30f);
        yield return Wait(1.3f);
        PlayAt(ropeCutClip, adel.transform.position, 1f);
        if (ropeTied != null) ropeTied.SetActive(false);
        if (ropeCut != null) ropeCut.SetActive(true);
        for (float e = 0f; e < 0.8f; e += Time.deltaTime) { tiedW = 1f - e / 0.8f; yield return null; }
        tiedW = 0f;
        yield return Wait(0.9f);

        // he turns on Khalid and shoves him: "All of you are trying to murder me!!"
        Vector3 toK = Flat(player.transform.position - adel.transform.position).normalized;
        Quaternion from = adel.transform.rotation, to = Quaternion.LookRotation(toK);
        for (float e = 0f; e < 0.55f; e += Time.deltaTime) { adel.transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, e / 0.55f)); yield return null; }
        adel.transform.rotation = to;
        // over Khalid's shoulder (the street side has a big bin, the other the pole): Adel shoves him back toward us
        Vector3 sideK = Vector3.Cross(Vector3.up, toK);
        if (Vector3.Dot(sideK, Flat(adel.transform.position - PoleBase)) < 0f) sideK = -sideK;
        SetShot(camA, player.transform.position + toK * 2.4f + sideK * 1.35f + Vector3.up * 1.45f, (adel.transform.position + player.transform.position) * 0.5f + Vector3.up * 1.0f, 44f, 30f);
        Say(adelLine, 3.4f);
        yield return Wait(0.35f);
        for (float e = 0f; e < 0.22f; e += Time.deltaTime) { pushW = e / 0.22f; yield return null; }
        pushW = 1f;
        PlayAt(shoveClip, player.transform.position, 1f);
        if (player.animator != null) player.animator.Play("GetHit", 0, 0f);
        lookAtSet = false;
        Vector3 pushTo = player.transform.position + toK * 0.75f;
        for (float e = 0f; e < 0.3f; e += Time.deltaTime)
        {
            var cc = player.controller;
            Vector3 step = toK * (0.75f / 0.3f) * Time.deltaTime;
            if (cc != null && cc.enabled) cc.Move(step + Vector3.down * 0.5f * Time.deltaTime); else player.transform.position += step;
            pushW = 1f - 0.3f * (e / 0.3f);
            yield return null;
        }
        for (float e = 0f; e < 0.35f; e += Time.deltaTime) { pushW = Mathf.Lerp(0.7f, 0f, e / 0.35f); yield return null; }
        pushW = 0f;
        yield return Wait(1.2f);

        // he runs off toward Block 3's side gate - over Khalid's shoulder
        adel.Play("Run", 0, 0f);
        adel.speed = AdelRunSpeed / AdelClipSpeed;
        if (adelSteps == null) adelSteps = gameObject.AddComponent<ScriptedFootsteps>();
        adelSteps.Track(adel, 0.6f);
        lookAt = adel.transform.position; lookAtSet = true;
        var path = new List<Vector3>(adelPath);
        if (path.Count == 0) path.Add(adel.transform.position);
        Vector3 k2 = player.transform.position;
        Vector3 toFirst = Flat(path[Mathf.Min(1, path.Count - 1)] - k2).normalized;
        SetShot(camB, k2 - toFirst * 2.2f + Vector3.Cross(Vector3.up, toFirst) * 0.8f + Vector3.up * 1.7f, path[Mathf.Min(1, path.Count - 1)] + Vector3.up * 0.9f, 50f, 45f);
        int seg = 0;
        float shotT = 0f;
        bool cutToGate = false;
        while (seg < path.Count)
        {
            float dt = Time.deltaTime;
            shotT += dt;
            Vector3 target = path[seg];
            Vector3 d = Flat(target - adel.transform.position);
            float stepLen = AdelRunSpeed * dt;
            if (d.magnitude <= stepLen) { adel.transform.position = Ground(target); seg++; }
            else
            {
                adel.transform.position = Ground(adel.transform.position + d.normalized * stepLen);
                adel.transform.rotation = Quaternion.RotateTowards(adel.transform.rotation, Quaternion.LookRotation(d), 540f * dt);
            }
            lookAt = adel.transform.position + Vector3.up * 0.8f;
            camB.LookAt = null;
            if (!cutToGate) camB.transform.rotation = Quaternion.Slerp(camB.transform.rotation, Quaternion.LookRotation(adel.transform.position + Vector3.up * 0.8f - camB.transform.position), 1f - Mathf.Exp(-6f * dt));
            // after a while: CUT to the gate, he's nearly there
            if (!cutToGate && shotT > 3.0f)
            {
                cutToGate = true;
                int last = path.Count - 1;
                if (last >= 1)
                {
                    Vector3 a = path[last - 1], bb = path[last];
                    float L = Flat(bb - a).magnitude;
                    Vector3 near = L > 6f ? Vector3.Lerp(bb, a, 6f / L) : a;
                    if (seg < last) { adel.transform.position = Ground(near); seg = last; adel.transform.rotation = Quaternion.LookRotation(Flat(bb - a)); }
                }
                if (gateShot != null) SetShot(camA, gateShot.position, gateShot.position + gateShot.forward * 5f, gateShot.GetComponent<Camera>() != null ? 50f : 52f, 40f);
            }
            yield return null;
        }

        // down on his belly and under the gate
        if (!cutToGate && gateShot != null) SetShot(camA, gateShot.position, gateShot.position + gateShot.forward * 5f, 52f, 40f);
        adelSteps.End();
        adel.Play("Idle", 0, 0f); adel.speed = 1f;
        crawlDir = Flat(crawlTo - adel.transform.position).normalized;
        if (crawlDir.sqrMagnitude < 0.01f) crawlDir = adel.transform.forward;
        adel.transform.rotation = Quaternion.LookRotation(crawlDir);
        Vector3 crawlStart = adel.transform.position;
        for (float e = 0f; e < 0.65f; e += Time.deltaTime) { crawlW = e / 0.65f; yield return null; }
        crawlW = 1f;
        float crawlLen = Flat(crawlTo - crawlStart).magnitude;
        for (float s = 0f; s < crawlLen; s += CrawlSpeed * Time.deltaTime)
        {
            adel.transform.position = Ground(crawlStart + crawlDir * s);
            crawlPhase += Time.deltaTime * 5.5f;
            yield return null;
        }
        // up again (standing where his head was) and off into Block 3
        Vector3 lying = adel.transform.position;
        for (float e = 0f; e < 0.75f; e += Time.deltaTime)
        {
            crawlW = 1f - e / 0.75f;
            adel.transform.position = Ground(lying + crawlDir * (1.0f * (e / 0.75f)));
            yield return null;
        }
        crawlW = 0f;
        adel.Play("Run", 0, 0f); adel.speed = AdelRunSpeed / AdelClipSpeed;
        adelSteps.Track(adel, 0.6f);
        float gone = 0f;
        int ai = 0;
        while (ai < afterGate.Count && gone < 1.4f)
        {
            float dt = Time.deltaTime; gone += dt;
            Vector3 d = Flat(afterGate[ai] - adel.transform.position);
            float stepLen = AdelRunSpeed * dt;
            if (d.magnitude <= stepLen) { adel.transform.position = Ground(afterGate[ai]); ai++; }
            else
            {
                adel.transform.position = Ground(adel.transform.position + d.normalized * stepLen);
                adel.transform.rotation = Quaternion.RotateTowards(adel.transform.rotation, Quaternion.LookRotation(d), 540f * dt);
            }
            yield return null;
        }
        adelSteps.End();
        adel.gameObject.SetActive(false);

        // "Adel wait!" - Khalid, looking where he went
        Vector3 kk = player.transform.position;
        Vector3 toGate = Flat(crawlTo - kk).normalized;
        player.transform.rotation = Quaternion.LookRotation(toGate);
        lookAt = crawlTo; lookAtSet = true;
        SetShot(camB, kk + toGate * 2.1f + Vector3.Cross(Vector3.up, toGate) * 0.9f + Vector3.up * 1.6f, kk + Vector3.up * 1.55f, 45f, 30f);
        yield return Wait(0.6f);
        Say(khalidLine, 2.6f);
        yield return Wait(3.0f);

        // gameplay again
        lookAtSet = false;
        EndCameras(0.8f);
        ReleaseKhalid(player);
        ShowGunAgain(player);
        StoryFlags.Set(DoneFlag);
        debugArmed = false;
        phase = Phase.Done; IsPlaying = false;
        yield return null; yield return null; yield return null;
        if (CheckpointManager.instance != null) CheckpointManager.instance.TriggerCheckpoint(checkpointName);
    }

    /// <summary>The blade, or the knife it was already combined into (both parts make ItemType.Knife).</summary>
    bool OwnsBlade(Player player)
    {
        foreach (var s in player.inventory.GetItems())
            if (s.item == knifeBlade || (s.item != null && s.item.itemType == ItemType.Knife)) return true;
        return false;
    }

    void TakeBlade(InspectableItemPickup blade)
    {
        if (blade == null) return;
        var player = Player.instance;
        if (player != null && player.inventory != null && blade.itemToPickup != null) player.inventory.PickUp(blade.itemToPickup, blade.itemQuantity);
        var clip = blade.pckupClip != null ? blade.pckupClip : pickupClip;
        if (clip != null) PlayAt(clip, blade.transform.position, 1f);
        else if (UISoundManager.instance != null) UISoundManager.instance.PlayButtonHoverSound();
        var save = blade.GetComponent<SaveablePickup>();
        if (save != null) save.MarkAsPickedUp();
        if (ItemInspectionHandler.instance != null) ItemInspectionHandler.instance.InspectableItemTriggerExit(blade);
        Destroy(blade.gameObject);
    }

    // ------------------------------------------------------------------ khalid

    void HoldKhalid(Player player, bool weaponToo)
    {
        if (player == null) return;
        if (!player.pauseMovement) { player.pauseMovement = true; pausedMovement = true; }
        var pm = player.GetComponent<PlayerMovement>();
        if (pm != null) pm.SetExternalMoveInput(Vector2.zero);
        if (weaponToo) Disable(player.playerWeaponSystem);
        Disable(player.GetComponent<PlayerRoll>());
        Disable(player.GetComponent<PlayerMeleeAttack>());
        Disable(player.GetComponent<DoorInteractionHandler>());
        if (player.health != null && !player.health.Invincible) { player.health.Invincible = true; madeInvincible = true; }
        if (player.animator != null) { player.animator.SetBool("Turning", false); player.animator.SetFloat("x", 0f); player.animator.SetFloat("y", 0f); }
    }

    void Disable(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false; disabled.Add(b);
    }

    void ReleaseKhalid(Player player)
    {
        scriptedMove = false;
        if (player != null)
        {
            if (pausedMovement) player.pauseMovement = false;
            if (madeInvincible && player.health != null) player.health.Invincible = false;
        }
        pausedMovement = false; madeInvincible = false;
        foreach (var b in disabled) if (b != null) b.enabled = true;
        disabled.Clear();
    }

    void HideGun(Player player)
    {
        var ws = player.playerWeaponSystem;
        if (ws == null) return;
        savedWeapon = ws.weaponIsEnabled;
        ws.weaponIsEnabled = false;
        if (gunEnabler == null) gunEnabler = player.GetComponent<PlayerWeaponEnabler>();
        if (gunEnabler != null && gunEnabler.WeaponObject != null) gunEnabler.WeaponObject.SetActive(false);
        gunHidden = true;
    }

    void ShowGunAgain(Player player)
    {
        if (!gunHidden || player == null) return;
        gunHidden = false;
        var ws = player.playerWeaponSystem;
        if (ws != null) ws.weaponIsEnabled = savedWeapon || (player.inventory != null && player.inventory.HasWeapon());
        if (gunEnabler != null && gunEnabler.WeaponObject != null && ws != null && ws.weaponIsEnabled && !PlayerMeleeWeapon.Equipped)
            gunEnabler.WeaponObject.SetActive(true);
    }

    void TeleportKhalid(Player player, Vector3 pos, Quaternion rot)
    {
        if (player.controller != null) player.controller.enabled = false;
        player.transform.SetPositionAndRotation(pos + Vector3.up * 0.05f, rot);
        if (player.controller != null) player.controller.enabled = true;
        player.OnPlayerTelported?.Invoke();
    }

    IEnumerator WalkKhalidTo(Vector3 target, float speed, float maxSeconds, Vector3 faceAtEnd)
    {
        moveTarget = target; moveSpeed = speed; scriptedMove = true;
        float t = 0f;
        var player = Player.instance;
        while (scriptedMove && t < maxSeconds && player != null && Flat(target - player.transform.position).magnitude > 0.12f) { t += Time.deltaTime; yield return null; }
        scriptedMove = false;
        if (player != null && player.animator != null) { player.animator.SetFloat("y", 0f); }
        lookAt = faceAtEnd; lookAtSet = true;
    }

    /// <summary>Scripted walk/run: moves his CharacterController (PlayerMovement is paused; it measures the real speed for
    /// the locomotion blend) and keeps the run/walk tree's 'y' up so it doesn't settle into idle.</summary>
    void MoveKhalid(Player p)
    {
        Vector3 d = Flat(moveTarget - p.transform.position);
        float dt = Time.deltaTime;
        if (d.magnitude < 0.12f) { scriptedMove = false; return; }
        Quaternion want = Quaternion.LookRotation(d);
        p.transform.rotation = Quaternion.RotateTowards(p.transform.rotation, want, 400f * dt);
        float step = Mathf.Min(d.magnitude, moveSpeed * dt);
        Vector3 move = p.transform.forward * step * Mathf.Clamp01(Vector3.Dot(p.transform.forward, d.normalized) + 0.2f);
        if (p.controller != null && p.controller.enabled) p.controller.Move(move + Vector3.down * 2f * dt);
        else p.transform.position += move;
        if (p.animator != null) { p.animator.SetFloat("x", 0f); p.animator.SetFloat("y", 1f); }
    }

    // ------------------------------------------------------------------ cameras

    void BeginCameras()
    {
        var cam = Camera.main;
        brain = cam != null ? cam.GetComponent<CinemachineBrain>() : null;
        if (brain != null && !blendSaved) { savedBlend = brain.m_DefaultBlend; blendSaved = true; }
        if (brain != null) brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.7f);
        live = null;
    }

    /// <summary>Cut (or, for the first shot, ease) to camera 'c' at 'pos' looking at 'target'.</summary>
    void SetShot(CinemachineVirtualCamera c, Vector3 pos, Vector3 target, float fov, float far)
    {
        if (brain != null) brain.m_DefaultBlend = live == null ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.7f)
                                                                : new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);
        c.LookAt = null; c.Follow = null;
        c.m_Lens.FieldOfView = fov; c.m_Lens.FarClipPlane = far; c.m_Lens.NearClipPlane = 0.08f;
        c.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos));
        var other = c == camA ? camB : camA;
        other.Priority = 0;
        c.Priority = 80;
        c.PreviousStateIsValid = false;
        live = c;
    }

    void EndCameras(float blend)
    {
        if (brain != null) brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, blend);
        camA.Priority = 0; camB.Priority = 0;
        live = null;
        StartCoroutine(Co_RestoreBlend(blend + 0.2f));
    }

    IEnumerator Co_RestoreBlend(float after)
    {
        yield return new WaitForSeconds(after);
        if (brain != null && blendSaved && live == null) brain.m_DefaultBlend = savedBlend;
    }

    IEnumerator Wait(float s) { for (float e = 0f; e < s; e += Time.deltaTime) yield return null; }

    IEnumerator Fade(bool toBlack, float s)
    {
        var f = FadeScreenUI.instance;
        if (f != null) { if (toBlack) f.FadeOut(s); else f.FadeIn(s); }
        yield return new WaitForSecondsRealtime(s);
    }

    // ------------------------------------------------------------------ late: facing + Adel's poses

    // a camera riding along with something (in its own yaw frame: x right, z ahead)
    Vector3 FollowPos()
    {
        Vector3 f = Flat(camFollow.forward).normalized, r = Vector3.Cross(Vector3.up, f);
        return camFollow.position + r * followOffset.x + Vector3.up * followOffset.y + f * followOffset.z;
    }

    void LateUpdate()
    {
        if (camFollow != null && live != null)
        {
            Vector3 want = FollowPos();
            live.transform.position = Vector3.Lerp(live.transform.position, want, 1f - Mathf.Exp(-8f * Time.deltaTime));
            live.transform.rotation = Quaternion.LookRotation(camFollow.position + followLook - live.transform.position);
        }
        var p = Player.instance;
        if (lookAtSet && p != null && !scriptedMove && (phase == Phase.Intro || phase == Phase.Outro))
        {
            Vector3 d = Flat(lookAt - p.transform.position);
            if (d.sqrMagnitude > 0.01f) p.transform.rotation = Quaternion.RotateTowards(p.transform.rotation, Quaternion.LookRotation(d), 260f * Time.deltaTime);
        }
        if (adel == null || !adel.gameObject.activeInHierarchy) return;
        PoseAdel();
    }

    void PoseAdel()
    {
        if (lUp == null || rUp == null) return;
        if (armA <= 0f)
        {
            armA = (lLo.position - lUp.position).magnitude; armB = (lHand.position - lLo.position).magnitude;
            if (lThigh != null) { legA = (lShin.position - lThigh.position).magnitude; legB = (lFoot.position - lShin.position).magnitude; }
        }
        Transform root = adel.transform;
        Vector3 fwd = root.forward, up = Vector3.up, right = root.right;

        if (crawlW > 0f)
        {
            // flat on his belly: the body pitched forward round his feet, a little off the ground
            root.rotation = Quaternion.LookRotation(crawlDir) * Quaternion.Euler(84f * crawlW, 0f, 0f);
            // height: the ground under his feet, middle and head together (he goes down into the hollow under the gate)
            Vector3 feet = root.position;
            float g = (Ground(feet).y + Ground(feet + crawlDir * 0.65f).y + Ground(feet + crawlDir * 1.25f).y) / 3f;
            root.position = new Vector3(feet.x, Mathf.Lerp(Ground(feet).y, g, crawlW) + 0.12f * crawlW, feet.z);
            Vector3 travel = crawlDir, side = Vector3.Cross(Vector3.up, travel);
            float ground = root.position.y - 0.12f * crawlW;
            float s = Mathf.Sin(crawlPhase);
            ArmIK(lUp, lLo, lHand, ShoulderGround(lUp, ground) + travel * (0.22f + 0.14f * s) - side * 0.1f, up * 0.6f - side * 0.8f, crawlW);
            ArmIK(rUp, rLo, rHand, ShoulderGround(rUp, ground) + travel * (0.22f - 0.14f * s) + side * 0.1f, up * 0.6f + side * 0.8f, crawlW);
            if (lThigh != null)
            {
                LegIK(lThigh, lShin, lFoot, ShoulderGround(lThigh, ground) - travel * (0.36f - 0.1f * s) - side * 0.12f + up * 0.08f, -up - side * 0.3f, crawlW);
                LegIK(rThigh, rShin, rFoot, ShoulderGround(rThigh, ground) - travel * (0.36f + 0.1f * s) + side * 0.12f + up * 0.08f, -up + side * 0.3f, crawlW);
            }
            return;
        }

        if (tiedW > 0f)
        {
            // hands round the pole behind him, struggling a little
            float wob = Mathf.Sin(Time.time * 2.3f) * 0.025f;
            Vector3 pc = PoleBase;
            float hy = root.position.y + 0.82f;
            Vector3 lT = new Vector3(pc.x, hy, pc.z) - right * (poleRadius + 0.035f) + fwd * (0.02f + wob);
            Vector3 rT = new Vector3(pc.x, hy, pc.z) + right * (poleRadius + 0.035f) + fwd * (0.02f - wob);
            ArmIK(lUp, lLo, lHand, lT, -right * 0.7f - fwd * 0.3f, tiedW);
            ArmIK(rUp, rLo, rHand, rT, right * 0.7f - fwd * 0.3f, tiedW);
        }
        if (pushW > 0f)
        {
            Vector3 lT = lUp.position + fwd * (armA + armB) * 0.92f - up * 0.05f + right * 0.04f;
            Vector3 rT = rUp.position + fwd * (armA + armB) * 0.92f - up * 0.05f - right * 0.04f;
            ArmIK(lUp, lLo, lHand, lT, -up - right * 0.3f, pushW);
            ArmIK(rUp, rLo, rHand, rT, -up + right * 0.3f, pushW);
        }
    }

    static Vector3 ShoulderGround(Transform b, float groundY) { var p = b.position; p.y = groundY + 0.03f; return p; }

    void ArmIK(Transform a, Transform b, Transform c, Vector3 target, Vector3 hint, float w) => TwoBone(a, b, c, target, hint, armA, armB, w);
    void LegIK(Transform a, Transform b, Transform c, Vector3 target, Vector3 hint, float w) => TwoBone(a, b, c, target, hint, legA, legB, w);

    /// <summary>Analytic two-bone IK in world space (axis-free: bones are aimed at their child joints).</summary>
    static void TwoBone(Transform a, Transform b, Transform c, Vector3 target, Vector3 hint, float la, float lb, float w)
    {
        if (a == null || b == null || c == null || w <= 0f) return;
        Vector3 root = a.position;
        Vector3 toT = target - root;
        float d = Mathf.Clamp(toT.magnitude, 0.01f, (la + lb) * 0.999f);
        Vector3 dir = toT.normalized;
        float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
        Vector3 bendAxis = Vector3.ProjectOnPlane(hint, dir);
        if (bendAxis.sqrMagnitude < 1e-6f) bendAxis = Vector3.ProjectOnPlane(Vector3.up, dir);
        bendAxis.Normalize();
        Vector3 elbow = root + dir * (la * cosA) + bendAxis * (la * Mathf.Sqrt(1f - cosA * cosA));
        Aim(a, b.position, elbow, w);
        Aim(b, c.position, root + dir * d, w);
    }

    static void Aim(Transform bone, Vector3 childNow, Vector3 childWant, float w)
    {
        Vector3 cur = childNow - bone.position, want = childWant - bone.position;
        if (cur.sqrMagnitude < 1e-8f || want.sqrMagnitude < 1e-8f) return;
        Quaternion delta = Quaternion.FromToRotation(cur, want);
        bone.rotation = Quaternion.Slerp(bone.rotation, delta * bone.rotation, w);
    }

    // ------------------------------------------------------------------ helpers

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    Vector3 Ground(Vector3 p)
    {
        var hits = Physics.RaycastAll(new Vector3(p.x, p.y + 2.5f, p.z), Vector3.down, 9f, ~0, QueryTriggerInteraction.Ignore);
        float bestY = float.NegativeInfinity;
        foreach (var h in hits)
        {
            if (Player.instance != null && h.collider.transform.IsChildOf(Player.instance.transform)) continue;
            if (adel != null && h.collider.transform.IsChildOf(adel.transform)) continue;
            if (runner != null && h.collider.transform.IsChildOf(runner.transform)) continue;
            var r = h.collider.GetComponent<Renderer>();
            if (r != null && !r.enabled && !(h.collider is TerrainCollider)) continue;     // invisible blockers
            if (h.point.y > bestY) bestY = h.point.y;
        }
        if (bestY > float.NegativeInfinity) p.y = bestY;
        return p;
    }

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

    void OnDisable()
    {
        if (barging) EndBarge();
        if (runnerDormant) SetRunnerDormant(false);
        foreach (var c in aggroBoxes) if (c != null) c.enabled = true;
        if (runnerHeld && runner != null) { runner.PauseEnemyState(false); if (runner.agent != null && runner.agent.isOnNavMesh) runner.agent.isStopped = false; }
        if (phase == Phase.Intro || phase == Phase.Outro || forceAim)
        {
            var p = Player.instance;
            SetForcedAim(p, false);
            ReleaseKhalid(p);
            if (brain != null && blendSaved) brain.m_DefaultBlend = savedBlend;
        }
        IsPlaying = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.2f);
        Gizmos.DrawCube(areaCenter, areaSize);
        Gizmos.color = Color.yellow;
        Vector3 a = khalidFreeSpot;
        foreach (var p in adelPath) { Gizmos.DrawLine(a, p); a = p; }
        Gizmos.DrawLine(a, crawlTo);
        a = crawlTo;
        foreach (var p in afterGate) { Gizmos.DrawLine(a, p); a = p; }
        Gizmos.color = Color.red;
        foreach (var s in runnerFarSpots) Gizmos.DrawSphere(s, 0.5f);
    }

    // ------------------------------------------------------------------ debug

    /// <summary>Debug (test key): Adel tied, Khalid a few steps short of the street (south side road) - walk in.</summary>
    public void DebugArmAndJump()
    {
        var p = Player.instance;
        if (!Application.isPlaying || p == null || phase == Phase.Intro || phase == Phase.Outro) return;
        debugArmed = true;
        phase = Phase.Waiting;
        if (adel != null) adel.gameObject.SetActive(false);
        IdleState();
        Vector3 at = Ground(new Vector3(-10.0f, 2f, -9.5f));        // the street east of the crossroads: walk west onto the road through it
        TeleportKhalid(p, at, Quaternion.Euler(0f, 270f, 0f));
    }

    public string DebugState => phase + " forceAim " + forceAim + " runnerDead " + RunnerDead + " armed " + Armed;
}
