using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Localization;

/// <summary>
/// The storage room meeting (store redesign, beat 5). Khalid walks in on the cashier, who stands with his back turned
/// putting the same carton on a shelf over and over. Khalid greets him and asks about the boy; no answer. The cashier
/// stops, lets the carton fall, turns round slowly with his mouth shut, the mouth splits open: "Where is he?".
/// He comes at Khalid, who backs into the tool shelf, finds the hammer and raises it. The fight starts.
///
/// Everything is a function of the cutscene time 't' (no Timeline asset): poses, cameras and one-shot events are looked up
/// from the constants below, so jumping in time (skip, debug) always gives a consistent state.
/// Built into the scene by Tools > Cutscenes > Build Storage Meeting.
/// </summary>
[DefaultExecutionOrder(150)]
public class StorageMeetingCutscene : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }
    public const string SaveKey = "store:storage-meeting";

    [Header("Actors and props")]
    [SerializeField] GameObject puppet;              // the cashier (QuestionerPuppet): model + animator only
    [SerializeField] QuestionerJawPose jaw;
    [SerializeField] Transform box;                  // the carton he keeps shelving
    [SerializeField] Enemy enemy;                    // the real Questioner, switched on where the cashier stands when the cutscene ends
    [SerializeField] GameObject hammerOnShelf;       // the hammer lying on the tool shelf (the old pickup object)
    [SerializeField] Item hammerItem;
    [SerializeField] CheckpointTrigger checkpoint;
    [SerializeField] GunTutorialPrompts tutorial;

    [Header("Marks (world)")]
    [SerializeField] Vector3 khalidTalk = new Vector3(-218.2f, 1.445f, 92.45f);
    [SerializeField] Vector3 khalidShelf = new Vector3(-216.2f, 1.445f, 92.82f);
    [SerializeField] Vector3 cashierSpot = new Vector3(-219.6f, 1.445f, 87.56f);
    [SerializeField] Vector3 slotOnShelf = new Vector3(-219.6f, 3.005f, 86.74f);     // carton centre when it sits in the rack
    [SerializeField] Vector3 hammerSpot = new Vector3(-215.15f, 2.4f, 93.5f);
    [SerializeField] float floorY = 1.445f;

    [Header("Feel")]
    [SerializeField] float stackSeconds = 6.6f;         // one carton up into the rack and back down
    [SerializeField] float cashierPace = 0.44f;          // m/s when he comes at Khalid
    [SerializeField] float cashierPaceAnim = 0.95f;
    [SerializeField] float headBowDegrees = 34f;
    [Tooltip("The Question (his scream) in the cutscene: seconds of sound-wave shells from his mouth, and of the blur / echo on screen.")]
    [SerializeField] float screamSeconds = 1.9f;
    [SerializeField] float screamEchoSeconds = 2.6f;
    [SerializeField] float skipHoldSeconds = 1.5f;

    [Header("Sound")]
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField] AudioClip boxSlide, boxDrop, shelfBump, whereIsHe;

    [Header("Lines (Subtitles table keys)")]
    [SerializeField] string line1 = "Peace be upon you.";
    [SerializeField] string line2 = "Friend, have you seen a boy with a backpack come through here?";
    [SerializeField] string line3 = "Alright... thanks anyway.";
    [SerializeField] string lineWhere = "Where is he?";
    [SerializeField] string line4 = "Get away from me! Get away!";

    // ---- timing (seconds from the start of the cutscene). Slow on purpose: every line gets silence after it, the turn
    // takes its time. (Placeholder pacing until the real performance is recorded.)
    const float TWalkIn = 0.8f, TWalkInEnd = 3.4f;
    const float TLine1 = 4.6f, TShotB = 8.2f, TLine2 = 10.2f, TShotC = 17.4f, TLine3 = 19.0f;
    const float TLeave = 22.6f;                                   // no answer: Khalid starts to turn back to the door
    const float TShotB2 = 23.6f, TStop = 24.4f, TDrop = 27.2f, TShotD = 29.2f, TTurn = 29.8f, TTurnEnd = 35.4f;
    const float TShotE = 36.2f, THeadUp = 36.0f, THeadUpEnd = 37.6f, TWindup = 38.0f, TOpen = 38.9f, TShotF = 42.0f, TCashierWalk = 42.3f;
    const float TBack = 42.8f, TBackEnd = 45.8f, TShotG = 46.0f, TGrab = 47.5f, TShotH = 47.5f, TLine4 = 47.9f, TEnd = 50.6f;

    bool running, done, skipping;
    float t, prevT, skipHeld;
    Animator puppetAnim, khalidAnim;
    Transform lUpper, lLower, lHand, rUpper, rLower, rHand, head;
    AudioSource cashierAudio, khalidAudio, cashierVoice;
    ScreamWaveEmitter waves;
    readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();
    readonly List<CinemachineVirtualCamera> shots = new List<CinemachineVirtualCamera>();
    CinemachineBrain brain;
    CinemachineBlendDefinition savedBlend;
    Vector3 khalidStart;
    float cashierYaw0 = 180f, cashierYaw, ikWeight = 1f, bow, boxDropVel;
    bool boxFalling, boxLanded;
    Vector3 boxFallPos;
    Quaternion boxFallRot;
    bool hammerGiven;

    Vector3 Fwd0 => Quaternion.Euler(0f, cashierYaw0, 0f) * Vector3.forward;

    void Awake()
    {
        if (puppet != null)
        {
            puppetAnim = puppet.GetComponentInChildren<Animator>(true);
            foreach (var tr in puppet.GetComponentsInChildren<Transform>(true))
            {
                switch (tr.name)
                {
                    case "LeftArm": lUpper = tr; break; case "LeftForeArm": lLower = tr; break; case "LeftHand": lHand = tr; break;
                    case "RightArm": rUpper = tr; break; case "RightForeArm": rLower = tr; break; case "RightHand": rHand = tr; break;
                    case "Head": head = tr; break;
                }
            }
        }
        cashierAudio = MakeSource("Cashier Audio", 1f, 2f, 14f);
        khalidAudio = MakeSource("Khalid Audio", 0.85f, 1.5f, 12f);
        // his voice rings on like the Questioner's scream in a fight (same echo), the carton sounds stay dry
        cashierVoice = MakeSource("Cashier Voice", 1f, 3f, 30f);
        var echo = cashierVoice.gameObject.AddComponent<AudioEchoFilter>();
        echo.delay = 230f; echo.decayRatio = 0.45f; echo.wetMix = 0.6f; echo.dryMix = 1f;
        // the shells of rippling air from his mouth: the same emitter + material the real Questioner uses
        var q = enemy as QuestionerEnemy;
        if (puppet != null && q != null && q.screamWaveMaterial != null)
        {
            waves = puppet.AddComponent<ScreamWaveEmitter>();
            waves.material = q.screamWaveMaterial;
            waves.origin = jaw != null ? jaw.jaw : null;
            waves.maxRadius = 5f;
        }
        BuildShots();
    }

    AudioSource MakeSource(string n, float spatial, float min, float max)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false; s.spatialBlend = spatial; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = min; s.maxDistance = max;
        s.dopplerLevel = 0f; s.outputAudioMixerGroup = sfxGroup;
        return s;
    }

    void Start()
    {
        done = SaveManager.instance != null && SaveManager.instance.IsInteractableUsed(SaveKey);
        if (done)
        {
            // loaded from a save made after the meeting: nobody at the shelf, the hammer is already his
            if (puppet != null) puppet.SetActive(false);
            if (box != null) box.gameObject.SetActive(false);
            if (hammerOnShelf != null) hammerOnShelf.SetActive(false);
            return;
        }
        if (puppet != null)
        {
            puppet.SetActive(true);
            puppet.transform.SetPositionAndRotation(cashierSpot, Quaternion.Euler(0f, cashierYaw0, 0f));
            if (puppetAnim != null) { puppetAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate; puppetAnim.SetFloat("Velocity", 0f); }
        }
        if (jaw != null) jaw.closed = 1f;
        cashierYaw = cashierYaw0;
        // the hammer is taken in the cutscene, not picked up by hand
        if (hammerOnShelf != null) foreach (var c in hammerOnShelf.GetComponents<Collider>()) c.enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || running || done) return;
        Begin();
    }

    // ------------------------------------------------------------------ start / end

    public void Begin()
    {
        var player = Player.instance;
        if (player == null || running || done) return;
        running = true; IsPlaying = true; t = prevT = 0f; skipHeld = 0f;

        // a checkpoint at the door first: dying in the fight comes back here and the meeting plays again
        if (checkpoint != null) checkpoint.Trigger();
        if (SaveManager.instance != null) SaveManager.instance.RegisterUsedInteractable(SaveKey);

        PlayerMeleeWeapon.SetCutscene(PlayerMeleeWeapon.CutsceneHold.Away);
        if (player.playerWeaponSystem != null) player.playerWeaponSystem.ExitOutOfAiming();
        Disable(player.GetComponent<PlayerMovement>());
        Disable(player.playerWeaponSystem);
        Disable(player.GetComponent<PlayerRoll>());
        Disable(player.GetComponent<PlayerMeleeAttack>());
        Disable(player.GetComponent<DoorInteractionHandler>());
        if (player.controller != null) player.controller.enabled = false;
        if (player.health != null) player.health.Invincible = true;
        if (CameraSystem.Instance != null) CameraSystem.Instance.ForceShowPlayer = true;

        khalidStart = player.transform.position;
        khalidAnim = null;
        foreach (var a in player.GetComponentsInChildren<Animator>(true)) if (a.name == "MainCharacterAnimated") khalidAnim = a;
        if (khalidAnim != null)
        {
            khalidAnim.SetFloat("Velocity", 0f); khalidAnim.SetFloat("x", 0f); khalidAnim.SetFloat("y", 0f); khalidAnim.SetBool("Turning", false);
            khalidAnim.Play("WalkMove", 0, 0f);
        }

        var cam = Camera.main;
        brain = cam != null ? cam.GetComponent<CinemachineBrain>() : null;
        if (brain != null) { savedBlend = brain.m_DefaultBlend; brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f); }
        ikWeight = 1f; bow = 0f; boxFalling = boxLanded = false; hammerGiven = false; cashierYaw = cashierYaw0;
        if (jaw != null) jaw.closed = 1f;
        Evaluate();
    }

    void Disable(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false; disabledBehaviours.Add(b);
    }

    void Finish(bool skipped)
    {
        var player = Player.instance;
        // where everybody ends up
        Vector3 cashierEnd = skipped || puppet == null ? CashierWalkPos(TEnd) : puppet.transform.position;
        if (player != null)
        {
            Vector3 look = cashierEnd - khalidShelf; look.y = 0f;
            player.transform.SetPositionAndRotation(khalidShelf, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.back));
        }
        GiveHammer();
        if (puppet != null) puppet.SetActive(false);
        if (box != null && !boxLanded) { box.SetPositionAndRotation(BoxRestOnFloor(), Quaternion.Euler(0f, 24f, 0f)); }
        SubtitleClose();

        if (enemy != null)
        {
            Vector3 p = cashierEnd;
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) || NavMesh.SamplePosition(p, out hit, 2f, NavMesh.AllAreas)) p = hit.position;
            Vector3 face = khalidShelf - p; face.y = 0f;
            enemy.transform.SetPositionAndRotation(p, Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face : Vector3.forward));
            enemy.gameObject.SetActive(true);
            StartCoroutine(Co_WakeEnemy());
        }

        foreach (var s in shots) if (s != null) s.Priority = 0;
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
        PlayerMeleeWeapon.SetCutscene(PlayerMeleeWeapon.CutsceneHold.None);
        if (PlayerMeleeWeapon.instance != null) PlayerMeleeWeapon.instance.Select();
        if (CameraSystem.Instance != null) CameraSystem.Instance.ForceShowPlayer = false;
        if (CutsceneSkipper.Instance != null) CutsceneSkipper.Instance.HideHint();
        if (tutorial != null) tutorial.StartTutorialNow();      // the controls are back: the prompts with them

        running = false; IsPlaying = false; done = true; skipping = false;
    }

    IEnumerator Co_WakeEnemy()
    {
        yield return null;
        if (enemy != null) enemy.ResetEnemy();
    }

    void OnDestroy()
    {
        if (running) IsPlaying = false;
    }

    // ------------------------------------------------------------------ per frame

    void Update()
    {
        if (done) return;
        if (!running) { IdleLoop(); return; }
        if (skipping) return;
        prevT = t;
        t += Time.deltaTime;
        Evaluate();
        if (t >= TEnd) { Finish(false); return; }
        HandleSkip();
    }

    /// <summary>Before Khalid walks in the cashier is already at it (and can be heard through the door once it opens).</summary>
    void IdleLoop()
    {
        if (puppet == null || box == null) return;
        float phase = Mathf.Repeat(Time.time, stackSeconds) / stackSeconds;
        box.SetPositionAndRotation(BoxLoopPos(phase), Quaternion.Euler(0f, cashierYaw0, 0f));
    }

    bool Crossed(float time) => prevT < time && t >= time;

    void Evaluate()
    {
        var player = Player.instance;
        if (player == null) return;

        // ---------------- the cashier
        Vector3 cPos = t < TCashierWalk ? cashierSpot : CashierWalkPos(t);
        float yawToKhalid = YawTo(cPos, t < TBack ? khalidTalk : player.transform.position);
        if (t < TTurn) cashierYaw = cashierYaw0;
        else if (t < TTurnEnd)
        {
            // over his left shoulder, in the six steps of the Q_Turn180 clip: the clip keeps the standing foot still against
            // exactly this turning of his root
            float delta = Mathf.DeltaAngle(cashierYaw0, yawToKhalid - 0.01f);
            if (delta > 90f) delta -= 360f;
            cashierYaw = cashierYaw0 + delta * TurnProfile((t - TTurn) / (TTurnEnd - TTurn));
        }
        else cashierYaw = Mathf.MoveTowardsAngle(cashierYaw, yawToKhalid, 140f * Time.deltaTime);
        if (puppet != null) puppet.transform.SetPositionAndRotation(cPos, Quaternion.Euler(0f, cashierYaw, 0f));
        if (puppetAnim != null)
        {
            float v = t >= TCashierWalk ? 1f : 0f;
            puppetAnim.SetFloat("Velocity", Mathf.MoveTowards(puppetAnim.GetFloat("Velocity"), v, 2.5f * Time.deltaTime));
            puppetAnim.speed = t >= TCashierWalk ? cashierPaceAnim : (t >= TOpen && t < TOpen + screamSeconds ? 0.55f : 1f);   // the scream held long
        }
        ikWeight = t < TDrop ? 1f : Mathf.Clamp01(1f - (t - TDrop) / 0.7f);
        bow = t < TStop ? 0f : t < THeadUp ? Smooth01((t - TStop) / 2.4f) : Mathf.Lerp(1f, 0.1f, Smooth01((t - THeadUp) / (THeadUpEnd - THeadUp)));
        // the mouth splits slowly through the wind-up and is wide open when the scream starts
        if (jaw != null) jaw.closed = t < TWindup ? 1f : 1f - Smooth01((t - TWindup) / (TOpen - TWindup + 0.25f));

        // ---------------- the carton
        if (box != null)
        {
            if (t < TStop)
            {
                float phase = Mathf.Repeat(t - TStop, stackSeconds) / stackSeconds;      // phase-locked: at TStop he is holding it at his chest
                box.SetPositionAndRotation(BoxLoopPos(phase), Quaternion.Euler(0f, cashierYaw0, 0f));
                if (boxSlide != null && Crossed(TStop - stackSeconds * (1f - 0.34f))) cashierAudio.PlayOneShot(boxSlide, 0.5f);
                if (boxSlide != null && Crossed(TStop - stackSeconds * (2f - 0.34f))) cashierAudio.PlayOneShot(boxSlide, 0.5f);
                if (boxSlide != null && Crossed(TStop - stackSeconds * (3f - 0.34f))) cashierAudio.PlayOneShot(boxSlide, 0.5f);
                if (boxSlide != null && Crossed(TStop - stackSeconds * (4f - 0.34f))) cashierAudio.PlayOneShot(boxSlide, 0.5f);
            }
            else if (t < TDrop) box.SetPositionAndRotation(BoxLoopPos(0f), Quaternion.Euler(0f, cashierYaw0, 0f));
            else if (!boxLanded)
            {
                if (!boxFalling) { boxFalling = true; boxDropVel = 0f; boxFallPos = BoxLoopPos(0f); boxFallRot = Quaternion.Euler(0f, cashierYaw0, 0f); }
                boxDropVel += 9.81f * Time.deltaTime;
                boxFallPos += Vector3.down * boxDropVel * Time.deltaTime;
                boxFallRot = Quaternion.Euler(-70f * Time.deltaTime, 0f, 25f * Time.deltaTime) * boxFallRot;
                float rest = floorY + 0.14f;
                if (boxFallPos.y <= rest)
                {
                    boxFallPos.y = rest; boxLanded = true;
                    boxFallRot = Quaternion.Euler(0f, cashierYaw0 + 24f, 0f);
                    if (boxDrop != null) cashierAudio.PlayOneShot(boxDrop, 0.9f);
                }
                box.SetPositionAndRotation(boxFallPos, boxFallRot);
            }
        }
        cashierAudio.transform.position = cPos + Vector3.up * 1.5f;
        cashierVoice.transform.position = cPos + Vector3.up * 1.7f;
        if (waves != null && t >= TOpen && t < TOpen + screamSeconds) waves.Emit();

        // ---------------- Khalid
        Vector3 kPos;
        if (t < TWalkIn) kPos = khalidStart;
        else if (t < TWalkInEnd) kPos = Vector3.Lerp(khalidStart, khalidTalk, Smooth01((t - TWalkIn) / (TWalkInEnd - TWalkIn)));
        else if (t < TBack) kPos = khalidTalk;
        else if (t < TBackEnd) kPos = Vector3.Lerp(khalidTalk, khalidShelf, Smooth01((t - TBack) / (TBackEnd - TBack)));
        else kPos = khalidShelf;
        kPos.y = player.transform.position.y;
        Vector3 moved = kPos - player.transform.position;
        bool leaving = t >= TLeave && t < TDrop + 0.55f;
        Vector3 lookDir = leaving ? khalidStart - kPos + new Vector3(0.9f, 0f, 0f) : cPos - kPos; lookDir.y = 0f;
        Quaternion kRot = lookDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(lookDir) : player.transform.rotation;
        float turnRate = leaving && t < TDrop ? 70f : t < TBack ? 200f : 240f;      // turning away slowly, spinning back at the thump
        player.transform.SetPositionAndRotation(kPos, Quaternion.RotateTowards(player.transform.rotation, kRot, turnRate * Time.deltaTime));
        if (khalidAnim != null)
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 local = Quaternion.Inverse(player.transform.rotation) * (moved / dt);
            float speed = new Vector2(local.x, local.z).magnitude;
            Vector2 dir = speed > 0.15f ? new Vector2(local.x, local.z) / speed : Vector2.zero;
            khalidAnim.SetFloat("Velocity", Mathf.MoveTowards(khalidAnim.GetFloat("Velocity"), Mathf.Min(speed, 2f), 6f * dt));
            khalidAnim.SetFloat("x", Mathf.MoveTowards(khalidAnim.GetFloat("x"), dir.x, 6f * dt));
            khalidAnim.SetFloat("y", Mathf.MoveTowards(khalidAnim.GetFloat("y"), dir.y, 6f * dt));
            khalidAnim.SetBool("Turning", false);
        }
        khalidAudio.transform.position = kPos + Vector3.up * 1.5f;
        if (Crossed(TBackEnd) && shelfBump != null) khalidAudio.PlayOneShot(shelfBump, 0.9f);
        if (Crossed(TGrab)) GiveHammer();
        PlayerMeleeWeapon.SetCutscene(t >= TShotH ? PlayerMeleeWeapon.CutsceneHold.Raised : PlayerMeleeWeapon.CutsceneHold.Away);

        // ---------------- lines
        if (Crossed(TLine1)) Say(line1, 2.8f);
        if (Crossed(TLine2)) Say(line2, 5.0f);
        if (Crossed(TLine3)) Say(line3, 3.0f);
        if (Crossed(TTurn) && puppetAnim != null && puppetAnim.HasState(0, TurnState)) puppetAnim.CrossFadeInFixedTime(TurnState, 0.2f, 0, 0f);
        if (Crossed(TTurnEnd) && puppetAnim != null) puppetAnim.CrossFadeInFixedTime("Movement", 0.35f);
        if (Crossed(TWindup) && puppetAnim != null) puppetAnim.CrossFadeInFixedTime("Q_ScreamWindup", 0.25f);
        if (Crossed(TOpen))
        {
            Say(lineWhere, 2.6f);
            if (whereIsHe != null) cashierVoice.PlayOneShot(whereIsHe, 1f);
            if (puppetAnim != null) puppetAnim.CrossFadeInFixedTime("Q_Scream", 0.12f);
            // what the scream does on screen in a fight (blur, echo, ringing), without slowing anybody
            var combat = player.GetComponent<PlayerCloseCombat>();
            if (combat != null) combat.ApplyScream(0f, 0f, screamEchoSeconds, 1f);
        }
        if (Crossed(TOpen + screamSeconds) && puppetAnim != null) puppetAnim.CrossFadeInFixedTime("Q_ScreamRecover", 0.2f);
        if (Crossed(TOpen + screamSeconds + 0.9f) && puppetAnim != null) puppetAnim.CrossFadeInFixedTime("Movement", 0.5f);
        if (Crossed(TLine4)) Say(line4, 2.4f);

        // ---------------- camera
        int shot = t < TShotB ? 0 : t < TShotC ? 1 : t < TShotB2 ? 2 : t < TShotD ? 1 : t < TShotE ? 3 : t < TShotF ? 4 : t < TShotG ? 5 : t < TShotH ? 6 : 7;
        UpdateShots(shot, cPos, kPos);
    }

    void LateUpdate()
    {
        if (done || puppet == null || !puppet.activeInHierarchy) return;
        // arms on the carton (after the Animator has posed him)
        if (ikWeight > 0.001f && box != null && lUpper != null && rUpper != null)
        {
            Vector3 right = box.right, up = Vector3.up, fwd = box.forward;
            Vector3 hintL = -up * 0.6f - right * 0.7f - fwd * 0.4f, hintR = -up * 0.6f + right * 0.7f - fwd * 0.4f;
            SolveArm(lUpper, lLower, lHand, box.position - right * 0.215f - up * 0.02f, hintL, ikWeight);
            SolveArm(rUpper, rLower, rHand, box.position + right * 0.215f - up * 0.02f, hintR, ikWeight);
        }
        // head hanging while he stands there and turns round
        if (head != null && bow > 0.001f) head.rotation = Quaternion.AngleAxis(headBowDegrees * bow, puppet.transform.right) * head.rotation;
    }

    // ------------------------------------------------------------------ helpers

    static readonly int TurnState = Animator.StringToHash("Q_Turn180");
    // the steps of Q_Turn180 (AnimationTools/questioner/q21_turn.py: STEPS, WIDEN), in frames of its 168
    static readonly float[] TurnStepFrom = { 14f, 46f, 76f, 102f, 128f, 150f }, TurnStepTo = { 38f, 68f, 96f, 122f, 146f, 163f };

    /// <summary>How far round his root is (0..1) at a point (0..1) of the turn: the same curve the clip was keyed against.</summary>
    static float TurnProfile(float u)
    {
        float f = Mathf.Clamp01(u) * 168f, sum = 0f;
        for (int i = 0; i < TurnStepFrom.Length; i++)
            sum += Smooth01((f - (TurnStepFrom[i] - 5f)) / (TurnStepTo[i] - TurnStepFrom[i] + 10f));
        return sum / TurnStepFrom.Length;
    }

    static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    static float YawTo(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from; d.y = 0f;
        return d.sqrMagnitude < 1e-4f ? 0f : Quaternion.LookRotation(d).eulerAngles.y;
    }

    /// <summary>The carton over one cycle: held at his chest, lifted, slid into the rack, left a moment, pulled out, lowered.</summary>
    Vector3 BoxLoopPos(float phase)
    {
        Vector3 hold = cashierSpot + Fwd0 * 0.43f + Vector3.up * 1.2f;
        Vector3 up = new Vector3(hold.x, slotOnShelf.y + 0.05f, hold.z);
        Vector3 inRack = slotOnShelf + Vector3.up * 0.012f;
        float[] k = { 0f, 0.12f, 0.30f, 0.48f, 0.60f, 0.78f, 0.93f, 1f };
        Vector3[] p = { hold, hold, up, inRack, inRack, up, hold, hold };
        for (int i = 0; i < k.Length - 1; i++)
            if (phase <= k[i + 1]) return Vector3.Lerp(p[i], p[i + 1], Smooth01((phase - k[i]) / Mathf.Max(1e-4f, k[i + 1] - k[i])));
        return hold;
    }

    Vector3 BoxRestOnFloor()
    {
        Vector3 hold = cashierSpot + Fwd0 * 0.43f;
        return new Vector3(hold.x, floorY + 0.14f, hold.z);
    }

    /// <summary>He walks from the rack toward where Khalid ends up, stopping a couple of metres short.</summary>
    Vector3 CashierWalkPos(float time)
    {
        Vector3 to = khalidShelf - cashierSpot; to.y = 0f;
        float maxDist = Mathf.Max(0f, to.magnitude - 2.3f);
        float d = Mathf.Clamp((time - TCashierWalk) * cashierPace, 0f, maxDist);
        // round the carton lying at his feet
        Vector3 side = Vector3.Cross(Vector3.up, to.normalized) * Mathf.Sin(Mathf.Clamp01(d / 1.6f) * Mathf.PI) * -0.35f;
        return cashierSpot + to.normalized * d + side;
    }

    static void SolveArm(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 hintDir, float weight)
    {
        if (upper == null || lower == null || hand == null) return;
        Quaternion u0 = upper.localRotation, l0 = lower.localRotation;
        Vector3 a = upper.position;
        float la = (lower.position - a).magnitude, lb = (hand.position - lower.position).magnitude;
        Vector3 at = target - a;
        float lt = Mathf.Clamp(at.magnitude, 0.02f, la + lb - 0.002f);
        Vector3 dir = at.normalized;
        float cosA = Mathf.Clamp((la * la + lt * lt - lb * lb) / (2f * la * lt), -1f, 1f);
        Vector3 bend = hintDir - dir * Vector3.Dot(hintDir, dir);
        bend = bend.sqrMagnitude < 1e-6f ? Vector3.down : bend.normalized;
        Vector3 elbow = a + dir * (la * cosA) + bend * (la * Mathf.Sqrt(1f - cosA * cosA));
        upper.rotation = Quaternion.FromToRotation(lower.position - a, elbow - a) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, (a + dir * lt) - lower.position) * lower.rotation;
        if (weight < 0.999f)
        {
            upper.localRotation = Quaternion.Slerp(u0, upper.localRotation, weight);
            lower.localRotation = Quaternion.Slerp(l0, lower.localRotation, weight);
        }
    }

    void GiveHammer()
    {
        if (hammerGiven) return;
        hammerGiven = true;
        if (hammerOnShelf != null) hammerOnShelf.SetActive(false);
        var player = Player.instance;
        if (player != null && hammerItem != null && player.inventory != null && !player.inventory.HasItem(hammerItem)) player.inventory.PickUp(hammerItem);
        if (PlayerMeleeWeapon.instance != null) PlayerMeleeWeapon.instance.Select();
    }

    void Say(string key, float seconds)
    {
        if (string.IsNullOrEmpty(key)) return;
        SubtitleManager.ShowSubtitle(new LocalizedString("Subtitles", key), false, null, seconds);
    }

    void SubtitleClose()
    {
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) SubtitleManager.instance.ForceClose();
    }

    // ------------------------------------------------------------------ cameras

    CinemachineVirtualCamera NewShot(string n, float fov)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var v = go.AddComponent<CinemachineVirtualCamera>();
        v.m_Lens.FieldOfView = fov; v.m_Lens.NearClipPlane = 0.08f;
        v.Priority = 0;
        v.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
        shots.Add(v);
        return v;
    }

    void BuildShots()
    {
        NewShot("Shot A wide from the door", 56f);
        NewShot("Shot B his back", 40f);
        NewShot("Shot C Khalid", 38f);
        NewShot("Shot D the turn", 30f);
        NewShot("Shot E the mouth", 27f);
        NewShot("Shot F he comes", 57f);
        NewShot("Shot G the shelf", 34f);
        NewShot("Shot H the hammer", 42f);
    }

    static void Aim(CinemachineVirtualCamera v, Vector3 pos, Vector3 lookAt)
    {
        v.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(lookAt - pos, Vector3.up));
    }

    void UpdateShots(int active, Vector3 cPos, Vector3 kPos)
    {
        float f = floorY;
        Vector3 cHead = head != null ? head.position : cPos + Vector3.up * 1.85f;
        Vector3 kHead = new Vector3(kPos.x, f + 1.62f, kPos.z);
        Vector3 toK = (khalidTalk - cashierSpot); toK.y = 0f; toK.Normalize();
        Vector3 sideK = Vector3.Cross(Vector3.up, toK);

        Aim(shots[0], new Vector3(-217.25f, f + 1.78f, 93.5f), new Vector3(-219.4f, f + 1.25f, 88.0f));
        Aim(shots[1], new Vector3(-217.55f, f + 1.62f, 89.55f), new Vector3(cashierSpot.x - 0.05f, f + 1.5f, cashierSpot.z - 0.45f));
        Aim(shots[2], new Vector3(-219.05f, f + 1.48f, 90.35f), kHead + Vector3.down * 0.12f);
        Aim(shots[3], cashierSpot + toK * 2.6f + sideK * 0.5f + Vector3.up * 1.62f, cashierSpot + Vector3.up * 1.72f);
        Aim(shots[4], cashierSpot + toK * 1.35f + sideK * 0.16f + Vector3.up * 1.7f, cHead + Vector3.down * 0.07f);
        Vector3 mid = (cPos + kPos) * 0.5f;
        Aim(shots[5], new Vector3(-215.0f, f + 1.45f, 89.35f), new Vector3(mid.x, f + 1.25f, mid.z));
        Aim(shots[6], new Vector3(-215.75f, f + 1.3f, 92.35f), hammerSpot + Vector3.down * 0.02f);
        Vector3 back = (cPos - khalidShelf); back.y = 0f; back.Normalize();
        Vector3 sideC = Vector3.Cross(Vector3.up, back);
        Aim(shots[7], cPos + back * 1.25f + sideC * 0.55f + Vector3.up * 1.75f, new Vector3(khalidShelf.x, f + 1.35f, khalidShelf.z));

        for (int i = 0; i < shots.Count; i++) shots[i].Priority = i == active ? 60 : 0;
    }

    // ------------------------------------------------------------------ skipping

    void HandleSkip()
    {
        var prompt = CutsceneSkipper.Instance;
        float holdTime = prompt != null ? prompt.HoldDuration : skipHoldSeconds;
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
        cashierAudio.Stop(); khalidAudio.Stop(); cashierVoice.Stop();
        var combat = Player.instance != null ? Player.instance.GetComponent<PlayerCloseCombat>() : null;
        if (combat != null) combat.ClearScream();
        Finish(true);
        if (FadeScreenUI.instance != null) FadeScreenUI.instance.FadeIn(0.5f);
    }

    /// <summary>Debug: play it now from the door (Play mode).</summary>
    [ContextMenu("Debug: Play Now")]
    public void DebugPlayNow()
    {
        var player = Player.instance;
        if (!Application.isPlaying || running || player == null) return;
        done = false;
        if (puppet != null) puppet.SetActive(true);
        if (box != null) box.gameObject.SetActive(true);
        if (player.controller != null) player.controller.enabled = false;
        player.transform.SetPositionAndRotation(new Vector3(-218.25f, floorY, 93.2f), Quaternion.Euler(0f, 180f, 0f));
        if (player.controller != null) player.controller.enabled = true;
        player.OnPlayerTelported?.Invoke();
        Begin();
    }

    /// <summary>Debug: skip it as if E had been held.</summary>
    public void DebugSkip() { if (running && !skipping) StartCoroutine(Co_Skip()); }

    /// <summary>Debug: jump to a time while it is playing.</summary>
    public void DebugSetTime(float time) { if (running) { prevT = time; t = time; } }
    public float DebugTime => t;
    public bool Running => running;
}
