using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Questioner with the spec moveset (Questioner spec, 2026-11): strafe approach, The Question (scream, with its echoing
/// sound-wave shells), the grab (an instant bite, countered by a pipe strike timed into the lunge), the double claw swipe,
/// a quick recoil from pipe strikes between moves, the kneeling stagger on a mouth hit, and one attacker at a time
/// (attack token: they take turns, the rest circle; hitting one out of turn makes him take it). The fight itself lives in QuestionerCombatState; this class owns the damage rules,
/// the token, the procedural mouth (jaw) and body flinch, and the mouth weak point (the gun destroys it on a hit).
/// specMoveset off = the old HeadshotEnemy behaviour.
/// </summary>
public class QuestionerEnemy : HeadshotEnemy
{
    [Header("Spec moveset")]
    public bool specMoveset = true;
    [Tooltip("A horde Questioner (the Alley 3 action sequence, QuestionerEnemyHorde.prefab): takes no turns - walks in and "
           + "screams whenever he likes (spaced out across the horde), and only a few at a time (hordeMaxGrabbers) grab when "
           + "close. No claws.")]
    public bool hordeMode;
    public QuestionerTuning tuning = new QuestionerTuning();

    [Header("Mouth (jaw bone, opened on top of the animation)")]
    [SerializeField] string jawBoneName = "Jaw";
    [Tooltip("Local axis the jaw opens around, and how far fully open is (deg).")]
    [SerializeField] Vector3 jawAxis = Vector3.right;
    [SerializeField] float jawMaxAngle = 28f;
    [Tooltip("Mouth opening at rest (spec: slightly open, 0.2).")]
    [SerializeField] float jawIdleOpen = 0.2f;

    [Header("Flinch (procedural, body hits don't stop him)")]
    [SerializeField] float flinchAngle = 9f;
    [SerializeField] float flinchStaggeredAngle = 14f;
    [SerializeField] float flinchReturn = 9f;

    [Header("Audio cues")]
    public AudioClip[] inhaleSFX;
    public AudioClip[] screamSFX;
    public AudioClip[] grabCueSFX;
    public AudioClip[] hissSFX;
    public AudioClip[] whisperSFX;
    public AudioClip[] biteSFX;
    public AudioClip[] swipeSFX;
    public AudioClip[] staggerSFX;
    [Range(0f, 2f)] public float cueVolume = 1f;

    [Header("Scream: echo + sound-wave shells")]
    [Tooltip("Custom/QuestionerScreamWave material for the shells rolling out from his mouth.")]
    public Material screamWaveMaterial;
    [SerializeField] float screamEchoDelay = 230f;
    [Range(0f, 1f)] [SerializeField] float screamEchoDecay = 0.45f;
    [Range(0f, 1f)] [SerializeField] float screamEchoWet = 0.6f;

    public QuestionerCombatState combatState;

    // ---- attack token: one Questioner attacks at a time, they take turns; the others circle ----
    static QuestionerEnemy tokenHolder;
    static float tokenFreeAt;
    static readonly List<QuestionerEnemy> all = new List<QuestionerEnemy>();
    static float nextHordeScream, nextHordeGrab, nextHordeClaw;
    static bool chorusDone, chorusLanded;
    static float nextChorusCheck, chorusFirstEngage = -1f;
    static bool chorusForced;
    static readonly List<QuestionerEnemy> chorusCrowd = new List<QuestionerEnemy>();
    static readonly RaycastHit[] viewBuffer = new RaycastHit[16];
    /// <summary>Every enabled Questioner (cheap to walk every frame, unlike FindObjectsByType with a horde of them).</summary>
    public static List<QuestionerEnemy> All => all;
    public static QuestionerEnemy TokenHolder => tokenHolder;
    public bool HasToken => tokenHolder == this;
    float turnEndedAt = float.NegativeInfinity;
    int provokedMoves;

    Transform jaw;
    Quaternion jawRest, jawBase, jawLastWritten;
    bool jawWritten;
    float jawOpen, jawTarget, jawTremble;
    Transform[] flinchBones;
    float flinch, flinchVel;
    Transform weakpointParent;
    GameObject weakpointTemplate;
    float weakpointBaseRadius = -1f;
    bool killedWhileStaggered;
    AudioSource cueSource, screamSource;
    ScreamWaveEmitter waves;

    public override void Awake()
    {
        base.Awake();
        combatState = new QuestionerCombatState(this, stateMachine);
    }

    public override void Start()
    {
        base.Start();
        health.OnDeath.AddListener(OnDied);       // after the base Die() listener: the death state is already entered
        health.damageFilter = FilterDamage;

        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == jawBoneName && t.gameObject.activeInHierarchy) jaw = t;
        }
        if (jaw != null) jawRest = jaw.localRotation;
        flinchBones = FindBones("Spine1", "Spine2", "Neck", "Head");
        jawOpen = jawTarget = jawIdleOpen;

        // the mouth weak point: keep a hidden copy, the gun destroys the original on a hit
        var hb = FindWeakpoint();
        if (hb != null)
        {
            weakpointParent = hb.transform.parent;
            weakpointTemplate = Instantiate(hb.gameObject, weakpointParent);
            weakpointTemplate.name = hb.gameObject.name + "_Spare";
            weakpointTemplate.SetActive(false);
            if (hb.TryGetComponent(out SphereCollider sc)) weakpointBaseRadius = sc.radius;
        }

        cueSource = gameObject.AddComponent<AudioSource>();
        cueSource.spatialBlend = 1f;
        cueSource.rolloffMode = AudioRolloffMode.Logarithmic;
        cueSource.minDistance = 2f;
        cueSource.maxDistance = 30f;
        var main = GetComponent<AudioSource>();
        if (main != null) cueSource.outputAudioMixerGroup = main.outputAudioMixerGroup;

        // the scream has its own source: it rings on in an echo (the other cues stay dry)
        var sg = new GameObject("ScreamVoice");
        sg.transform.SetParent(jaw != null ? jaw : transform, false);
        screamSource = sg.AddComponent<AudioSource>();
        screamSource.playOnAwake = false;
        screamSource.spatialBlend = 1f;
        screamSource.rolloffMode = AudioRolloffMode.Logarithmic;
        screamSource.minDistance = 3f;
        screamSource.maxDistance = 40f;
        screamSource.outputAudioMixerGroup = cueSource.outputAudioMixerGroup;
        var echo = sg.AddComponent<AudioEchoFilter>();
        echo.delay = screamEchoDelay;
        echo.decayRatio = screamEchoDecay;
        echo.wetMix = screamEchoWet;
        echo.dryMix = 1f;

        waves = gameObject.AddComponent<ScreamWaveEmitter>();
        waves.material = screamWaveMaterial;
        waves.origin = jaw;
        waves.maxRadius = tuning.screamRadius + 0.5f;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (health != null) health.OnDeath.RemoveListener(OnDied);
        ReleaseToken();
    }

    void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    void OnDisable()
    {
        all.Remove(this);
        ReleaseToken();
        if (combatState != null) combatState.ForceReleasePlayer();
    }

    Transform[] FindBones(params string[] names)
    {
        var list = new List<Transform>();
        foreach (var n in names)
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == n && t.gameObject.activeInHierarchy) { list.Add(t); break; }
        return list.ToArray();
    }

    Hitbox FindWeakpoint()
    {
        foreach (var h in GetComponentsInChildren<Hitbox>(true))
            if (h.damageType == DamageType.Weakpoint && h.gameObject.activeSelf && h.gameObject != weakpointTemplate && h.gameObject.activeInHierarchy)
                return h;
        return null;
    }

    // ---------------- token ----------------

    public bool TryTakeToken()
    {
        if (tokenHolder == this) return true;
        if (tokenHolder != null && tokenHolder.CanHoldToken) return false;
        if (Time.time < tokenFreeAt) return false;
        // turns: whoever has waited longest goes next
        foreach (var other in all)
            if (other != this && other.CanHoldToken && other.turnEndedAt < turnEndedAt) return false;
        tokenHolder = this;
        provokedMoves = 0;
        return true;
    }

    /// <summary>Horde: a grab is free while fewer than hordeMaxGrabbers of the horde are already in one, nobody is holding
    /// Khalid, and the horde's break after the last grab (hordeGrabGap) is over - without it the grabs came back to back.</summary>
    public bool TryHordeGrab()
    {
        if (Time.time < nextHordeGrab || (Player.instance != null && (Player.instance.isGrabbed || Player.instance.isKnockedDown))) return false;
        int grabbing = 0;
        foreach (var other in all)
            if (other != this && other.hordeMode && other.stateMachine.CurrentState == other.combatState && other.combatState.InGrabMove) grabbing++;
        return grabbing < tuning.hordeMaxGrabbers;
    }

    /// <summary>Horde: the claws, for the ones beside / behind Khalid - the same rules as the grab (a few at a time, nobody
    /// holding him, a horde-wide break after each).</summary>
    public bool TryHordeClaw()
    {
        if (Time.time < nextHordeClaw || (Player.instance != null && Player.instance.isGrabbed)) return false;
        int clawing = 0;
        foreach (var other in all)
            if (other != this && other.hordeMode && other.stateMachine.CurrentState == other.combatState && other.combatState.InClawMove) clawing++;
        return clawing < tuning.hordeMaxClawers;
    }

    public void HordeClawEnded()
    {
        nextHordeClaw = Mathf.Max(nextHordeClaw, Time.time + Random.Range(tuning.hordeClawGapMin, tuning.hordeClawGapMax));
    }

    /// <summary>Horde: one of the horde's grabs is over (caught him, missed or countered): the break starts.</summary>
    public void HordeGrabEnded()
    {
        nextHordeGrab = Mathf.Max(nextHordeGrab, Time.time + Random.Range(tuning.hordeGrabGapMin, tuning.hordeGrabGapMax));
        // no claws in his back the moment he has shoved one off
        nextHordeClaw = Mathf.Max(nextHordeClaw, Time.time + tuning.hordeClawAfterGrab);
    }

    /// <summary>A new horde sequence (ItemPickupEnemySpawner starting): its chorus can happen again.</summary>
    public static void ResetHordeChorus()
    {
        chorusDone = chorusLanded = chorusForced = false;
        chorusFirstEngage = -1f;
    }

    /// <summary>One of the chorus line-up: stands still where the spawner put him, waiting for Khalid, until the chorus.</summary>
    public bool InLineUp { get; private set; }
    public void JoinLineUp() => InLineUp = true;

    /// <summary>The chorus opens the fight: nobody in the horde screams, grabs or claws before it, so Khalid sees it with a clear
    /// view and the normal camera. If it can't happen (too few of them get close) they start anyway after hordeChorusMaxHold.</summary>
    public bool HordeMayAttack
    {
        get
        {
            if (chorusDone) return true;
            if (chorusFirstEngage < 0f) chorusFirstEngage = Time.time;
            return Time.time - chorusFirstEngage > tuning.hordeChorusMaxHold;
        }
    }

    /// <summary>Horde, once per sequence. With a line-up (ItemPickupEnemySpawner put them on the road ahead of Khalid): when he
    /// is within hordeChorusTriggerRange of it and can see hordeChorusMin of them, or within hordeChorusCloseRange whatever he
    /// is looking at, or when he hits one. Without one: hordeChorusMin of the horde within hordeChorusTriggerRange. All of the
    /// line-up and every other horde Questioner within hordeChorusRange then scream at once. True if it started.</summary>
    public bool TryHordeChorus()
    {
        if (chorusDone || Time.time < nextChorusCheck) return false;
        nextChorusCheck = Time.time + 0.25f;
        var cam = Camera.main;
        var p = Player.instance;
        if (cam == null || p == null || p.isGrabbed) return false;
        Vector3 at = p.transform.position;
        float r2 = tuning.hordeChorusRange * tuning.hordeChorusRange;
        float trigger2 = tuning.hordeChorusTriggerRange * tuning.hordeChorusTriggerRange;
        chorusCrowd.Clear();
        Vector3 lineUpAt = Vector3.zero;
        int lineUp = 0, seen = 0, close = 0;
        foreach (var o in all)
        {
            if (!o.hordeMode || o.health == null || o.health.IsDead) continue;
            bool free = o.stateMachine.CurrentState == o.chaseState
                        || (o.stateMachine.CurrentState == o.combatState && o.combatState.CanJoinChorus);
            if (o.InLineUp)
            {
                lineUp++;
                lineUpAt += o.transform.position;
                if (free) chorusCrowd.Add(o);
                if (ClearlyInView(cam, o)) seen++;
                continue;
            }
            float d2 = (o.transform.position - at).sqrMagnitude;
            if (d2 > r2) continue;
            if (free) chorusCrowd.Add(o);
            if (d2 <= trigger2) close++;
        }

        bool fire;
        if (lineUp > 0)
        {
            Vector3 toLineUp = lineUpAt / lineUp - at;
            toLineUp.y = 0f;
            float d = toLineUp.magnitude;
            fire = chorusForced || (d <= tuning.hordeChorusTriggerRange && seen >= tuning.hordeChorusMin) || d <= tuning.hordeChorusCloseRange;
        }
        else fire = close >= tuning.hordeChorusMin;
        if (!fire) return false;

        chorusDone = true;
        chorusLanded = false;
        foreach (var o in all) o.InLineUp = false;
        chorusCrowd.Sort((a, b) => (a.transform.position - at).sqrMagnitude.CompareTo((b.transform.position - at).sqrMagnitude));
        int loud = 0;
        foreach (var o in chorusCrowd)
        {
            // the ones still walking up join in too
            if (o.stateMachine.CurrentState == o.chaseState) o.stateMachine.ChangeState(o.combatState);
            if (o.stateMachine.CurrentState != o.combatState) continue;
            o.combatState.StartChorus(tuning.hordeChorusHold + Random.Range(0f, tuning.hordeChorusSpread), loud < tuning.hordeChorusVoices, loud < tuning.hordeChorusWaves);
            loud++;
        }
        // a moment of quiet after it before the usual screams pick up again
        nextHordeScream = Time.time + tuning.hordeChorusQuiet;
        return true;
    }

    /// <summary>On screen with nothing solid between him and the camera.</summary>
    static bool ClearlyInView(Camera cam, QuestionerEnemy o)
    {
        Vector3 chest = o.transform.position + Vector3.up * 1.2f;
        Vector3 vp = cam.WorldToViewportPoint(chest);
        if (vp.z <= 0f || vp.x < 0.02f || vp.x > 0.98f || vp.y < 0f || vp.y > 1f) return false;
        Vector3 eye = cam.transform.position;
        Vector3 dir = chest - eye;
        int count = Physics.RaycastNonAlloc(eye, dir.normalized, viewBuffer, dir.magnitude, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var c = viewBuffer[i].collider;
            if (c == null || c.GetComponentInParent<Player>() != null || c.GetComponentInParent<Enemy>() != null) continue;
            return false;
        }
        return true;
    }

    /// <summary>The chorus hits Khalid once (the first of them to scream).</summary>
    public bool ClaimChorusHit()
    {
        if (chorusLanded) return false;
        chorusLanded = true;
        return true;
    }

    /// <summary>Horde: screams are spaced out across the whole horde (twenty starting in one frame is a wall of noise).</summary>
    public bool TryHordeScream()
    {
        if (Time.time < nextHordeScream) return false;
        nextHordeScream = Time.time + tuning.hordeScreamSpacing;
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetTurns()
    {
        tokenHolder = null;
        tokenFreeAt = 0f;
        nextHordeScream = nextHordeGrab = nextHordeClaw = nextChorusCheck = 0f;
        chorusDone = chorusLanded = chorusForced = false;
        chorusFirstEngage = -1f;
        all.Clear();
    }

    public void ReleaseToken()
    {
        if (tokenHolder == this) tokenHolder = null;
    }

    /// <summary>An attack is over: his turn passes to the next Questioner after `gap` seconds - unless he's alone in the fight
    /// (keeps it) or was provoked (keeps it for provokedTurnMoves moves). True if he gave it up.</summary>
    public bool EndTurn(float gap)
    {
        if (tokenHolder != this) return false;
        if (provokedMoves > 0 && --provokedMoves > 0) return false;
        bool waiting = false;
        foreach (var other in all) if (other != this && other.CanHoldToken) { waiting = true; break; }
        if (!waiting) return false;
        turnEndedAt = Time.time;
        tokenHolder = null;
        tokenFreeAt = Time.time + gap;
        return true;
    }

    /// <summary>Khalid hit him while it wasn't his turn: he takes it there and then (the one attacking loses it, and goes
    /// next) and stays on Khalid for provokedTurnMoves moves.</summary>
    void Provoke()
    {
        if (hordeMode || tokenHolder == this || stateMachine.CurrentState != combatState || combatState.IsStaggered) return;
        tokenHolder = this;
        tokenFreeAt = 0f;
        provokedMoves = tuning.provokedTurnMoves;
        PlayCue(hissSFX);
        combatState.OnProvoked();
    }

    bool CanHoldToken => this != null && !hordeMode && isActiveAndEnabled && health != null && !health.IsDead
                         && stateMachine.CurrentState == combatState && !combatState.IsStaggered;

    // ---------------- state flow ----------------

    public override void CheckLeaveCondition(EnemyState currentState)
    {
        if (!specMoveset) { base.CheckLeaveCondition(currentState); return; }

        // the line-up waits in the fight state, standing still (see QuestionerCombatState.UpdateFree)
        if (InLineUp && !health.IsDead && (currentState == idleState || currentState == chaseState))
        {
            stateMachine.ChangeState(combatState);
            return;
        }

        // wherever the old rules would start the old attack, the fight starts instead
        if ((currentState == idleState && stats.enemyType == EnemyType.Fixed) || currentState == enemyWanderState)
        {
            if (currentState.canLeave && PlayerInAttackRange()) { stateMachine.ChangeState(combatState); return; }
            if (currentState == idleState) return;
        }
        // the chase / sidestep hand over to the fight once he's close and can see Khalid
        if (currentState == chaseState || currentState == attackState || currentState == enemySidestepState)
        {
            if (currentState.canLeave && combatState.WantsToEngage())
            {
                stateMachine.ChangeState(combatState);
                return;
            }
            if (currentState == attackState) return;     // (never runs the old swipe)
        }
        base.CheckLeaveCondition(currentState);
    }

    /// <summary>Running into the swing: the pipe reaches him a little further out during the grab lunge (the counter window).</summary>
    public override float MeleeReachBonus => specMoveset && stateMachine.CurrentState == combatState && combatState.IsCounterable ? tuning.counterReachBonus : 0f;

    public override bool IsDownedForMelee => base.IsDownedForMelee || (specMoveset && stateMachine.CurrentState == combatState && combatState.IsStaggered);

    /// <summary>The first mouth shot staggers him rather than killing him: it does a body shot's damage at most and always
    /// leaves him standing (the gun's weak-point multiplier would otherwise one-shot him). The shot while he's down kills.</summary>
    int FilterDamage(int dmg)
    {
        if (!specMoveset || !health.isDamageByWeakpointHit || pendingEnvironmentHit) return dmg;
        bool staggered = stateMachine.CurrentState == combatState && combatState.IsStaggered;
        if (staggered) return dmg;
        return Mathf.Clamp(Mathf.Min(dmg, tuning.staggerShotDamage), 0, Mathf.Max(0, health.GetHealthValue() - 1));
    }

    public override void TakeDamage(int dmgValue)
    {
        if (!specMoveset) { base.TakeDamage(dmgValue); return; }
        if (health.IsDead) return;
        if (InLineUp) chorusForced = true;     // Khalid started it

        // the action sequence's blast (DangerZone): the full damage and the old weakpoint knock-down (falls, lies, gets up),
        // not the kneeling hold-the-mouth stagger of the player's mouth shot
        if (pendingEnvironmentHit)
        {
            pendingMeleeHit = false;
            base.TakeDamage(dmgValue);
            health.isDamageByWeakpointHit = false;
            return;
        }

        bool melee = pendingMeleeHit;
        pendingMeleeHit = false;
        bool weak = health.isDamageByWeakpointHit;
        health.isDamageByWeakpointHit = false;
        ConsumeKnockbackScale();
        PlaySoundEffect(stats.takeDamageSFX);

        bool staggered = stateMachine.CurrentState == combatState && combatState.IsStaggered;
        if (weak)
        {
            if (staggered)
            {
                // second mouth hit while he's down: kill
                killedWhileStaggered = true;
                health.Damage(health.GetHealthValue());
                return;
            }
            if (stateMachine.CurrentState != combatState) stateMachine.ChangeState(combatState);
            combatState.BeginStagger();
            return;
        }

        // body hit: a flinch, he keeps coming; a pipe strike hurts him (a recoil between moves, a counter in the grab lunge)
        Flinch(staggered ? flinchStaggeredAngle : (melee ? flinchAngle * 1.6f : flinchAngle));
        if (melee && Player.instance != null)
        {
            if (stateMachine.CurrentState != combatState && !staggered) stateMachine.ChangeState(combatState);
            Provoke();
            if (stateMachine.CurrentState == combatState) combatState.OnMeleeHit();
            return;
        }
        Provoke();
        if (stateMachine.CurrentState == idleState || stateMachine.CurrentState == enemyWanderState || stateMachine.CurrentState == investigateState)
        {
            playerDetected = true;
            stateMachine.ChangeState(chaseState);
        }
    }

    void OnDied()
    {
        ReleaseToken();
        combatState.ForceReleasePlayer();
        jawTarget = killedWhileStaggered ? 0.5f : 0.8f;
        // from the stagger he falls forward from his knees (hands stay over the mouth)
        if (killedWhileStaggered || combatState.IsStaggered) anim.SetInteger("DeadIndex", 2);
    }

    public override void AlertToSound(Vector3 soundPosition)
    {
        if (specMoveset && stateMachine.CurrentState == combatState) return;
        base.AlertToSound(soundPosition);
    }

    public override void OnPool()
    {
        base.OnPool();
        killedWhileStaggered = false;
        RestoreWeakpoint();
        SetWeakpointScale(1f);
    }

    // ---------------- mouth weak point ----------------

    public void RestoreWeakpoint()
    {
        if (weakpointTemplate == null || FindWeakpoint() != null) return;
        var copy = Instantiate(weakpointTemplate, weakpointParent);
        copy.name = weakpointTemplate.name.Replace("_Spare", "");
        copy.SetActive(true);
    }

    /// <summary>The scream wind-up opens the mouth wide: the weak point gets bigger (best moment to shoot).</summary>
    public void SetWeakpointScale(float scale)
    {
        var hb = FindWeakpoint();
        if (hb != null && weakpointBaseRadius > 0f && hb.TryGetComponent(out SphereCollider sc)) sc.radius = weakpointBaseRadius * scale;
    }

    // ---------------- mouth + flinch (on top of the animation) ----------------

    public void SetMouth(float open, float tremble = 0f)
    {
        jawTarget = open;
        jawTremble = tremble;
    }

    public void ResetMouth() { SetMouth(jawIdleOpen); }

    public void Flinch(float degrees)
    {
        flinchVel += degrees * 14f;
    }

    void LateUpdate()
    {
        if (!specMoveset) return;
        float dt = Time.deltaTime;

        if (jaw != null)
        {
            // only the move clips key the jaw: under his old idle / walk clips the animator leaves our last write in place,
            // and opening on top of that again every frame spun the jaw round - so open from the last ANIMATED value
            Quaternion cur = jaw.localRotation;
            if (!jawWritten || Quaternion.Angle(cur, jawLastWritten) > 0.01f) jawBase = cur;
            jawOpen = Mathf.MoveTowards(jawOpen, jawTarget, dt * 3.5f);
            float j = jawOpen + (jawTremble > 0f ? (Mathf.PerlinNoise(Time.time * 18f, 3.1f) - 0.5f) * jawTremble : 0f);
            jaw.localRotation = jawBase * Quaternion.AngleAxis(Mathf.Max(0f, j) * jawMaxAngle, jawAxis);
            jawLastWritten = jaw.localRotation;
            jawWritten = true;
        }

        // damped spring: kicks back, settles
        flinchVel += (-flinch * flinchReturn * flinchReturn - flinchVel * 2f * flinchReturn) * dt;
        flinch += flinchVel * dt;
        if (Mathf.Abs(flinch) > 0.01f && flinchBones != null)
        {
            Vector3 axis = transform.right;
            for (int i = 0; i < flinchBones.Length; i++)
            {
                float w = i < 2 ? 0.35f : 0.25f;
                flinchBones[i].rotation = Quaternion.AngleAxis(-flinch * w, axis) * flinchBones[i].rotation;
            }
        }
    }

    // ---------------- audio ----------------

    public void PlayCue(AudioClip[] clips, float volume = 1f)
    {
        if (clips == null || clips.Length == 0 || cueSource == null) return;
        var c = clips[Random.Range(0, clips.Length)];
        if (c != null) cueSource.PlayOneShot(c, volume * cueVolume);
    }

    public void StopCues()
    {
        if (cueSource != null) cueSource.Stop();
        if (screamSource != null) screamSource.Stop();
    }

    public void PlayScream(float volume = 1f)
    {
        if (screamSFX == null || screamSFX.Length == 0 || screamSource == null) return;
        var c = screamSFX[Random.Range(0, screamSFX.Length)];
        if (c != null) screamSource.PlayOneShot(c, volume * cueVolume);
    }

    /// <summary>Called every frame while he screams: a shell of rippling air rolls out from his mouth every so often.</summary>
    public void EmitScreamWave(bool big = false)
    {
        if (waves == null) return;
        if (big) waves.EmitBig();
        else waves.Emit();
    }
}

[System.Serializable]
public class QuestionerTuning
{
    [Header("Movement (spec: walk 1.0, strafe 1.2 sideways, direction every 0.8-1.6 s)")]
    public float walkSpeed = 1.0f;
    public float strafeSpeed = 1.2f;
    public float strafeSwitchMin = 0.8f, strafeSwitchMax = 1.6f;
    public float acceleration = 3.5f;
    public float turnSpeed = 220f;
    [Tooltip("Takes over from the chase within this distance (with line of sight); gives it back beyond disengageRange.")]
    public float engageRange = 9f, disengageRange = 12f;
    [Tooltip("Without the token: circle Khalid at this radius, tightening from max to min.")]
    public float circleMax = 6f, circleMin = 4f, circleTighten = 0.12f;
    [Tooltip("Turns: after each attack the token passes to the Questioner that has waited longest, after this gap (s); after "
           + "one that hurt Khalid the gap is afterHitRepositionMin.")]
    public float turnGap = 0.8f;
    [Tooltip("Hit while it isn't his turn: he takes the turn at once and keeps it for this many moves.")]
    public int provokedTurnMoves = 2;

    [Header("Horde (hordeMode)")]
    [Tooltip("Walks in to this distance (m) and holds there, screaming.")]
    public float hordeStopDistance = 3.2f;
    [Tooltip("At most this many of the horde in a grab at once; they grab from this range (m).")]
    public int hordeMaxGrabbers = 2;
    public float hordeGrabMin = 1.8f, hordeGrabMax = 5.5f;
    [Tooltip("After a horde grab ends no one in the horde grabs for this long (s).")]
    public float hordeGrabGapMin = 8f, hordeGrabGapMax = 12f;
    [Tooltip("Only the ones in front of Khalid (within this angle of the way he's running, or facing when he stands) grab.")]
    public float hordeFrontAngle = 50f;
    [Tooltip("The rest use the claws: at most this many at once, from this range (m) - they run in to claw distance.")]
    public int hordeMaxClawers = 2;
    public float hordeClawRange = 4.5f, hordeClawRunSpeed = 2.6f;
    [Tooltip("After a horde claw attack ends no one in the horde claws for this long (s).")]
    public float hordeClawGapMin = 5f, hordeClawGapMax = 8f;
    [Tooltip("After a horde grab ends no one in the horde claws for at least this long (s).")]
    public float hordeClawAfterGrab = 3f;
    [Tooltip("Seconds between two horde screams starting (across the whole horde).")]
    public float hordeScreamSpacing = 0.6f;
    [Tooltip("Each horde Questioner's own scream cooldown (s).")]
    public float hordeScreamCooldownMin = 4f, hordeScreamCooldownMax = 8f;
    [Tooltip("The chorus (once per sequence, opens the fight): Khalid within hordeChorusTriggerRange (m) of the line-up and seeing "
           + "this many of them, or within hordeChorusCloseRange (m) of it whatever he looks at (no line-up: this many of the horde "
           + "within hordeChorusTriggerRange). The line-up and every other one within hordeChorusRange (m) scream at once, each "
           + "hordeChorusHold (s) + up to hordeChorusSpread (s) late; the nearest hordeChorusVoices voice it, the "
           + "nearest hordeChorusWaves throw the huge shells; the scream reaches Khalid hordeChorusHitDelay (s) in (he sees the "
           + "waves first); then hordeChorusQuiet (s) of no screams.")]
    public int hordeChorusMin = 8;
    public float hordeChorusTriggerRange = 9f, hordeChorusCloseRange = 6f, hordeChorusRange = 14f, hordeChorusSpread = 0.1f, hordeChorusHold = 0.6f;
    public int hordeChorusVoices = 6, hordeChorusWaves = 24;
    public float hordeChorusHitDelay = 0.02f, hordeChorusQuiet = 2.5f;
    [Tooltip("No horde attacks before the chorus - unless it hasn't happened this long (s) after the first of them engaged.")]
    public float hordeChorusMaxHold = 45f;

    [Header("Decisions")]
    public float firstMoveDelay = 0.6f;
    public float decisionInterval = 0.35f;
    public float moveGapMin = 0.5f, moveGapMax = 1.2f;
    [Range(0f, 1f)] public float screamChance = 0.5f, grabChance = 0.6f;

    [Header("The Question (scream)")]
    public float screamMin = 3f, screamMax = 8f, screamCooldown = 10f;
    public float screamWindup = 0.8f, screamActive = 1.0f, screamRecovery = 0.6f;
    public float screamRadius = 4f;
    [Range(0f, 1f)] public float screamSlow = 0.3f;
    [Tooltip("The slow lasts this long (s); the blur / echo, aim sway and ringing last screamEffectTime.")]
    public float screamSlowTime = 1.0f;
    public float screamEffectTime = 6.0f;
    [Tooltip("While Khalid aims down the sights the scream reaches him this far out (m), and it's his first answer to it.")]
    public float screamAimRange = 12f;
    [Range(0f, 1f)] public float adsScreamChance = 0.85f;
    public float weakpointWindupScale = 1.7f;

    [Header("Grab")]
    public float grabMin = 4f, grabMax = 6f, grabCooldown = 8f;
    public float grabWindup = 0.6f, rushSpeed = 4f, rushMaxDistance = 6f;
    public float grabContact = 0.95f;
    public int biteDamage = 25;
    [Tooltip("Caught: holds him (s), then screams into his face (the scream lands at grabScreamAt, the clip runs grabScreamTime).")]
    public float grabHoldPause = 0.35f;
    public float grabScreamAt = 0.45f, grabScreamTime = 1.45f;
    [Tooltip("The slow bite: lands at biteLandTime (s), gnaws until biteHoldTime, then Khalid shoves him back pushOffDistance (m).")]
    public float biteLandTime = 0.7f, biteHoldTime = 1.45f;
    public float pushOffDistance = 1.4f;
    public float holdDistance = 0.62f;
    public float stumbleTime = 1.0f, grabMissRecovery = 0.9f;
    [Tooltip("The counter: a pipe strike landing in the last part of the wind-up (s) or in the rush knocks him back this far (m).")]
    public float counterWindow = 0.3f, counterPushDistance = 1.3f;
    [Tooltip("He runs into the swing: extra pipe reach (m) while the counter window is open.")]
    public float counterReachBonus = 0.35f;

    [Header("Swipe")]
    public float swipeRange = 1.5f;
    public float swipeWindup = 0.4f, swipeGap = 0.3f, swipeRecovery = 0.7f;
    public int swipeDamage = 10;
    public float swipeReach = 1.85f, swipeArc = 110f;

    [Header("Stagger (a gun shot in the mouth: the melee window)")]
    public float staggerTime = 4.0f, staggerRecovery = 0.8f;
    [Tooltip("Damage of the mouth shot that staggers him (spec: no extra damage; a body shot is 25). Never kills.")]
    public int staggerShotDamage = 25;

    [Header("Hurt (a pipe strike between moves or in the claws)")]
    [Tooltip("The recoil (s), then always the feint into the grab (never the claws).")]
    public float hurtTime = 0.45f;
    [Tooltip("The feint: backs off and drifts sideways for a random time (s) to a random distance (m), then grabs.")]
    public float feintTimeMin = 0.8f, feintTimeMax = 2.4f;
    public float feintDistMin = 2.2f, feintDistMax = 4.8f;

    [Header("Backing off")]
    [Tooltip("After hurting Khalid he circles without attacking for this long (s): time to recover and get ready.")]
    public float afterHitRepositionMin = 1.8f, afterHitRepositionMax = 2.8f;
    [Tooltip("His swipe shoves Khalid back (m).")]
    public float swipePushPlayer = 0.55f;
}
