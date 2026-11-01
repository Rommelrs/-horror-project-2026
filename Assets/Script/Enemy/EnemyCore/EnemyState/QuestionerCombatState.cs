using UnityEngine;

/// <summary>
/// The Questioner's fight (spec moveset). One state with its own phases, driven by timers (spec numbers in QuestionerTuning):
///  Free     - token holder: walks at Khalid while strafing left / right (direction every 0.8-1.6 s); others circle at 6 -> 4 m.
///  Scream   - wind-up 0.8 (inhale, mouth splits, weak point bigger) / active 1.0 (within 4 m: Khalid slowed 30% + aim sway
///             1 s) / recovery 0.6.
///  Grab     - wind-up 0.6 (hunch, arms up) / rush 4 m/s straight, max 6 m (a roll's i-frames slip it, a shove stops it) /
///             hold 1.5 s (mash Shove to push him off: he stumbles 1 s) / bite 25 and release.
///  Swipe    - wind-up 0.4 / two claw swipes 0.3 s apart, 10 each / recovery 0.7.
///  Stagger  - mouth hit: knees, hands over the mouth 3 s, stands up 0.8. Melee does double, a second mouth hit kills.
/// Movement uses NavMeshAgent.Move (stays on the navmesh, no path), he always faces Khalid; the animator gets the local
/// velocity (MoveX / MoveZ) for the Q_Locomotion blend tree and plays the move states by name.
/// </summary>
public class QuestionerCombatState : EnemyState
{
    enum Phase { Free, Pushed, ScreamWindup, ScreamActive, ScreamRecover, GrabWindup, GrabRush, GrabHold, Bite, Release, Stumble, Swipe, StaggerDown, StaggerLoop, StaggerUp }

    readonly QuestionerEnemy q;
    QuestionerTuning T => q.tuning;
    Phase phase;
    float phaseT;
    Vector3 vel;
    int strafeDir = 1;
    float strafeTimer;
    float circleRadius;
    int circleDir = 1;
    float nextDecision, nextScream, nextGrab, freeSince;
    float noSightTime;
    Vector3 rushDir, rushStart;
    float blockedTime;
    bool rushDodged;
    int swipeHits;
    bool landedFirst;
    bool holdingPlayer, escaped, bitten;
    float stumbleBack;
    Vector3 pushVel;
    float pushDecay;
    float repositionUntil, repositionDist;
    bool repositionAttacks;
    bool hurtPlayer;

    public bool IsStaggered => phase == Phase.StaggerDown || phase == Phase.StaggerLoop || phase == Phase.StaggerUp;
    public bool IsGrabbing => phase == Phase.GrabHold || phase == Phase.Bite;
    public string PhaseName => phase.ToString();

    static readonly int MoveX = Animator.StringToHash("MoveX");
    static readonly int MoveZ = Animator.StringToHash("MoveZ");

    public QuestionerCombatState(QuestionerEnemy enemy, EnemyStateMachine sm) : base(enemy, sm)
    {
        q = enemy;
    }

    Transform PlayerT => Player.instance != null ? Player.instance.transform : null;

    Vector3 ToPlayerFlat()
    {
        Vector3 d = PlayerT.position - q.transform.position;
        d.y = 0f;
        return d;
    }

    public bool WantsToEngage()
    {
        if (PlayerT == null || Player.instance.IsDead()) return false;
        return ToPlayerFlat().magnitude <= T.engageRange && q.HasLineOfSightToPlayer();
    }

    // ---------------- enter / exit ----------------

    public override void Enter()
    {
        canLeave = false;
        if (!q.agent.enabled) q.agent.enabled = true;
        if (!q.agent.isOnNavMesh) q.agent.Warp(q.transform.position);
        if (q.agent.isOnNavMesh) { q.agent.ResetPath(); q.agent.isStopped = true; }
        q.agent.updateRotation = false;
        q.agent.velocity = Vector3.zero;
        q.anim.SetBool("Strafing", false);
        vel = Vector3.zero;
        circleRadius = T.circleMax;
        circleDir = Random.value < 0.5f ? 1 : -1;
        strafeDir = Random.value < 0.5f ? 1 : -1;
        strafeTimer = Random.Range(T.strafeSwitchMin, T.strafeSwitchMax);
        float now = Time.time;
        nextDecision = now + T.firstMoveDelay;
        nextScream = Mathf.Max(nextScream, now + T.firstMoveDelay);
        nextGrab = Mathf.Max(nextGrab, now + T.firstMoveDelay);
        noSightTime = 0f;
        q.ResetMouth();
        SetPhase(Phase.Free, "Q_Locomotion", 0.25f);
        q.OnAttackStarted?.Invoke();
    }

    public override void Exit()
    {
        ForceReleasePlayer();
        q.SetWeakpointScale(1f);
        q.ResetMouth();
        q.ReleaseToken();
        q.agent.updateRotation = true;
        if (q.agent.isOnNavMesh) q.agent.isStopped = false;
        q.anim.SetFloat(MoveX, 0f);
        q.anim.SetFloat(MoveZ, 0f);
        if (!q.health.IsDead) q.anim.CrossFadeInFixedTime("Movement", 0.2f, 0);
    }

    void SetPhase(Phase p, string animState = null, float fade = 0.15f)
    {
        phase = p;
        phaseT = 0f;
        if (animState != null) q.anim.CrossFadeInFixedTime(animState, fade, 0);
    }

    // ---------------- update ----------------

    public override void Update()
    {
        if (PlayerT == null) return;
        if (Player.instance.IsDead() && phase == Phase.Free)
        {
            stateMachine.ChangeState(q.idleState);
            return;
        }
        float dt = Time.deltaTime;
        phaseT += dt;
        Vector3 toP = ToPlayerFlat();
        float d = toP.magnitude;
        Vector3 dir = d > 0.01f ? toP / d : q.transform.forward;

        // a pipe strike's shove, whatever he's doing (not while holding Khalid or down on his knees)
        if (pushVel.sqrMagnitude > 0.0001f)
        {
            if (!IsGrabbing && !IsStaggered) MoveAgent(pushVel * dt);
            pushVel = Vector3.MoveTowards(pushVel, Vector3.zero, pushDecay * dt);
        }

        switch (phase)
        {
            case Phase.Free: UpdateFree(dt, d, dir); break;

            case Phase.Pushed:
                // knocked back a step, still facing Khalid - then he backs off and circles instead of walking straight back in
                vel = Vector3.zero;
                Face(dir, 200f);
                SendLocomotion(pushVel);
                if (phaseT >= 0.35f)
                {
                    // if he'd just hurt Khalid, the "let him recover" backing off wins (no attacks)
                    bool calm = hurtPlayer;
                    hurtPlayer = false;
                    if (calm) BeginReposition(false, Random.Range(T.afterHitRepositionMin, T.afterHitRepositionMax), Random.Range(3.0f, 4.0f));
                    else BeginReposition(true, Random.Range(T.hitRepositionMin, T.hitRepositionMax), Random.Range(T.hitRepositionDistMin, T.hitRepositionDistMax));
                }
                break;

            case Phase.ScreamWindup:
                Brake(dt); Face(dir, 360f);
                if (phaseT >= T.screamWindup)
                {
                    SetPhase(Phase.ScreamActive, "Q_Scream", 0.1f);
                    q.SetMouth(1.15f, 0.25f);
                    q.PlayCue(q.screamSFX, 1.2f);
                    if (d <= T.screamRadius && q.HasLineOfSightToPlayer() && PlayerCloseCombat.instance != null)
                        PlayerCloseCombat.instance.ApplyScream(T.screamSlow, T.screamEffectTime);
                    q.SetWeakpointScale(1f);
                }
                break;

            case Phase.ScreamActive:
                Brake(dt);
                if (phaseT >= T.screamActive)
                {
                    SetPhase(Phase.ScreamRecover, "Q_ScreamRecover", 0.2f);
                    q.ResetMouth();
                }
                break;

            case Phase.ScreamRecover:
                Brake(dt);
                if (phaseT >= T.screamRecovery) EndMove();
                break;

            case Phase.GrabWindup:
                Brake(dt); Face(dir, 400f);
                if (phaseT >= T.grabWindup)
                {
                    rushDir = dir;
                    rushStart = q.transform.position;
                    blockedTime = 0f;
                    rushDodged = false;
                    vel = rushDir * T.rushSpeed * 0.6f;
                    SetPhase(Phase.GrabRush, "Q_GrabRush", 0.08f);
                    q.SetMouth(0.7f);
                }
                break;

            case Phase.GrabRush: UpdateRush(dt, d, dir); break;

            case Phase.GrabHold:
                HoldPlayer();
                if (PlayerCloseCombat.instance != null && PlayerCloseCombat.instance.EscapeComplete)
                {
                    escaped = true;
                    ReleasePlayer(true);
                    BeginStumble(1.0f);
                }
                else if (phaseT >= T.escapeWindow)
                {
                    SetPhase(Phase.Bite, "Q_Bite", 0.1f);
                    q.SetMouth(1.2f);
                    bitten = false;
                }
                break;

            case Phase.Bite:
                HoldPlayer();
                if (!bitten && phaseT >= 0.22f)
                {
                    bitten = true;
                    q.PlayCue(q.biteSFX, 1.1f);
                    if (PlayerCloseCombat.instance != null) PlayerCloseCombat.instance.OnBitten();
                    Player.instance.health.Damage(T.biteDamage);
                    hurtPlayer = true;
                    q.SetMouth(0.3f);
                }
                if (phaseT >= 0.6f)
                {
                    ReleasePlayer(false);
                    // lets go with a shove: Khalid staggers back out of his reach
                    if (PlayerCloseCombat.instance != null) PlayerCloseCombat.instance.Knockback(q.transform.forward, T.bitePushPlayer, 0.3f);
                    SetPhase(Phase.Release, "Q_Locomotion", 0.3f);
                    q.ResetMouth();
                }
                break;

            case Phase.Release:
                Brake(dt); Face(dir, 120f);
                if (phaseT >= 0.8f) EndMove();
                break;

            case Phase.Stumble:
            {
                // pushed back over the first 0.35 s, then catching himself
                float k = Mathf.Clamp01(1f - phaseT / 0.35f);
                Vector3 back = -q.transform.forward * stumbleBack * 2f * k / 0.35f;
                MoveAgent(back * dt);
                vel = Vector3.zero;
                SendLocomotion(Vector3.zero);
                if (phaseT >= T.stumbleTime) EndMove();
                break;
            }

            case Phase.Swipe: UpdateSwipe(dt, d, dir); break;

            case Phase.StaggerDown:
                Brake(dt);
                if (phaseT >= 0.5f) SetPhase(Phase.StaggerLoop, "Q_StaggerLoop", 0.15f);
                break;

            case Phase.StaggerLoop:
                Brake(dt);
                if (phaseT >= T.staggerTime - 0.5f)
                {
                    SetPhase(Phase.StaggerUp, "Q_StaggerUp", 0.2f);
                    q.ResetMouth();
                }
                break;

            case Phase.StaggerUp:
                Brake(dt);
                if (phaseT >= T.staggerRecovery)
                {
                    q.RestoreWeakpoint();
                    EndMove();
                }
                break;
        }
        KeepOffPlayer();
    }

    /// <summary>NavMeshAgent.Move ignores Khalid's capsule: never let the bodies overlap (except while he's holding him).</summary>
    void KeepOffPlayer()
    {
        if (IsGrabbing || stateMachine.CurrentState != this) return;
        Vector3 toP = ToPlayerFlat();
        float d = toP.magnitude;
        const float minGap = 0.75f;
        if (d >= minGap) return;
        Vector3 away = d > 0.01f ? -toP / d : -q.transform.forward;
        MoveAgent(away * (minGap - d));
    }

    // ---------------- free movement + decisions ----------------

    void UpdateFree(float dt, float d, Vector3 dir)
    {
        // lost him: back to the normal chase
        noSightTime = q.HasLineOfSightToPlayer() ? 0f : noSightTime + dt;
        if (d > T.disengageRange || noSightTime > 2f)
        {
            canLeave = true;
            stateMachine.ChangeState(q.stats.enemyType == EnemyType.Fixed ? (EnemyState)q.idleState : q.chaseState);
            return;
        }

        Vector3 right = Vector3.Cross(Vector3.up, dir);
        Vector3 desired;
        bool token = q.TryTakeToken();
        bool repositioning = Time.time < repositionUntil;
        if (token && repositioning)
        {
            // backing off to his distance and circling (after a pipe strike, or after he hurt Khalid)
            strafeTimer -= dt;
            if (strafeTimer <= 0f)
            {
                strafeDir = strafeDir == 0 ? (Random.value < 0.5f ? 1 : -1) : -strafeDir;
                strafeTimer = Random.Range(T.strafeSwitchMin, T.strafeSwitchMax);
            }
            if (strafeDir == 0) strafeDir = 1;
            float radial = Mathf.Clamp((d - repositionDist) * 1.2f, -T.walkSpeed * 1.2f, T.walkSpeed * 0.5f);
            desired = dir * radial + right * (strafeDir * T.strafeSpeed * 0.85f);
        }
        else if (token)
        {
            strafeTimer -= dt;
            if (strafeTimer <= 0f)
            {
                strafeDir = strafeDir == 0 ? (Random.value < 0.5f ? 1 : -1) : (Random.value < 0.15f ? 0 : -strafeDir);
                strafeTimer = Random.Range(T.strafeSwitchMin, T.strafeSwitchMax);
            }
            // closes to swiping distance, steps back if Khalid is right on him
            float stop = T.swipeRange * 0.8f;
            float fwd = T.walkSpeed * Mathf.Clamp((d - stop) / 0.8f, -0.6f, 1f);
            float side = T.strafeSpeed * strafeDir * Mathf.Clamp01((d - 2.2f) / 1.5f);
            desired = dir * fwd + right * side;
            circleRadius = T.circleMax;
        }
        else
        {
            circleRadius = Mathf.Max(T.circleMin, circleRadius - T.circleTighten * dt);
            float radial = Mathf.Clamp((d - circleRadius) * 0.8f, -T.walkSpeed * 0.6f, T.walkSpeed);
            desired = dir * radial + right * (circleDir * T.strafeSpeed * 0.55f);
        }
        desired += Separation();

        vel = Vector3.MoveTowards(vel, desired, T.acceleration * dt);
        MoveAgent(vel * dt);
        Face(dir, T.turnSpeed);
        SendLocomotion(vel);

        if (!token || Time.time < nextDecision) return;
        nextDecision = Time.time + T.decisionInterval;
        bool sight = q.HasLineOfSightToPlayer();
        // letting Khalid recover after hurting him: no attacks at all; after a pipe strike: only the grab / scream, from range
        if (repositioning && !repositionAttacks) return;
        float grabMin = repositioning ? Mathf.Min(T.grabMin, 3.0f) : T.grabMin;

        if (d <= T.swipeRange && !repositioning)
        {
            StartSwipe();
        }
        else if (sight && Time.time >= nextGrab && d >= grabMin && d <= T.grabMax && Random.value < T.grabChance)
        {
            nextGrab = Time.time + T.grabCooldown;
            SetPhase(Phase.GrabWindup, "Q_GrabWindup", 0.15f);
            q.PlayCue(q.grabCueSFX);
            q.SetMouth(0.45f);
        }
        else if (sight && Time.time >= nextScream && d >= T.screamMin && d <= T.screamMax && Random.value < T.screamChance)
        {
            nextScream = Time.time + T.screamCooldown;
            SetPhase(Phase.ScreamWindup, "Q_ScreamWindup", 0.15f);
            q.PlayCue(q.inhaleSFX);
            q.SetMouth(1f);
            q.SetWeakpointScale(T.weakpointWindupScale);
        }
    }

    Vector3 Separation()
    {
        Vector3 push = Vector3.zero;
        foreach (var e in Object.FindObjectsByType<QuestionerEnemy>(FindObjectsSortMode.None))
        {
            if (e == q || e.health == null || e.health.IsDead) continue;
            Vector3 away = q.transform.position - e.transform.position;
            away.y = 0f;
            float m = away.magnitude;
            if (m > 0.01f && m < 1.4f) push += away / m * (1.4f - m) * 1.5f;
        }
        return push;
    }

    void EndMove()
    {
        q.SetWeakpointScale(1f);
        q.ResetMouth();
        vel = Vector3.zero;
        if (hurtPlayer)
        {
            // he got Khalid: give him room to recover and get ready again
            hurtPlayer = false;
            BeginReposition(false, Random.Range(T.afterHitRepositionMin, T.afterHitRepositionMax), Random.Range(3.0f, 4.0f));
            return;
        }
        SetPhase(Phase.Free, "Q_Locomotion", 0.25f);
        nextDecision = Time.time + Random.Range(T.moveGapMin, T.moveGapMax);
    }

    void BeginReposition(bool attacksAllowed, float seconds, float distance)
    {
        repositionUntil = Time.time + seconds;
        repositionDist = distance;
        repositionAttacks = attacksAllowed;
        strafeDir = Random.value < 0.5f ? 1 : -1;
        strafeTimer = Random.Range(T.strafeSwitchMin, T.strafeSwitchMax);
        SetPhase(Phase.Free, "Q_Locomotion", 0.2f);
        nextDecision = Time.time + 0.5f;
    }

    /// <summary>A pipe strike: shoves him back a step. Between moves (or in a swipe's recovery) it also stops him and he backs
    /// off to circle - grab / scream allowed, no walking straight back in. Wind-ups and the rush keep going (pushed only).</summary>
    public void OnMeleeHit(Vector3 away)
    {
        if (IsStaggered || IsGrabbing) return;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = -q.transform.forward;
        away.Normalize();
        const float pushTime = 0.3f;
        float v0 = 2f * T.meleePushDistance / pushTime;
        pushVel = away * v0;
        pushDecay = v0 / pushTime;
        bool between = phase == Phase.Free || phase == Phase.Release || phase == Phase.ScreamRecover || phase == Phase.Stumble
                       || phase == Phase.Pushed || (phase == Phase.Swipe && swipeHits >= 2);
        if (!between) return;
        q.SetWeakpointScale(1f);
        q.ResetMouth();
        vel = Vector3.zero;
        SetPhase(Phase.Pushed, "Q_Locomotion", 0.1f);
    }

    // ---------------- grab ----------------

    void UpdateRush(float dt, float d, Vector3 dir)
    {
        vel = Vector3.MoveTowards(vel, rushDir * T.rushSpeed, 30f * dt);
        Vector3 before = q.transform.position;
        MoveAgent(vel * dt);
        float moved = (q.transform.position - before).magnitude;
        blockedTime = moved < vel.magnitude * dt * 0.35f ? blockedTime + dt : 0f;
        Face(rushDir, 90f);

        // contact: Khalid in front of him and close
        Vector3 toP = PlayerT.position - q.transform.position;
        toP.y = 0f;
        var pc = PlayerCloseCombat.instance;
        bool dodging = pc != null && pc.IsInvulnerable;
        // dodged as he arrived: this rush is spent, he carries on past and skids
        if (dodging && toP.magnitude <= 2.5f) rushDodged = true;
        if (rushDodged && toP.magnitude < 0.95f)
        {
            // ran into where Khalid was: skids to a stop against him
            SetPhase(Phase.Release, "Q_Locomotion", 0.2f);
            q.ResetMouth();
            phaseT = 0.8f - T.grabMissRecovery;
            vel = Vector3.zero;
            return;
        }
        if (!dodging && !rushDodged && toP.magnitude <= T.grabContact && Vector3.Angle(q.transform.forward, toP) < 75f && pc != null && pc.CanBeGrabbed)
        {
            holdingPlayer = true;
            escaped = false;
            pc.BeginGrab(q, T.escapeWindow, T.escapePresses);
            SetPhase(Phase.GrabHold, "Q_GrabHold", 0.08f);
            q.SetMouth(0.85f, 0.15f);
            q.PlayCue(q.whisperSFX, 1.1f);
            vel = Vector3.zero;
            return;
        }

        float travelled = (q.transform.position - rushStart).magnitude;
        if (travelled >= T.rushMaxDistance || blockedTime > 0.15f)
        {
            // missed: skids to a stop
            SetPhase(Phase.Release, "Q_Locomotion", 0.25f);
            q.ResetMouth();
            phaseT = 0.8f - T.grabMissRecovery;
        }
    }

    void HoldPlayer()
    {
        Brake(Time.deltaTime);
        if (PlayerCloseCombat.instance != null && holdingPlayer)
            PlayerCloseCombat.instance.HoldAt(q.transform.position + q.transform.forward * T.holdDistance, q.transform);
    }

    void ReleasePlayer(bool pushedOff)
    {
        if (!holdingPlayer) return;
        holdingPlayer = false;
        if (PlayerCloseCombat.instance != null) PlayerCloseCombat.instance.EndGrab(pushedOff);
    }

    public void ForceReleasePlayer()
    {
        ReleasePlayer(false);
    }

    void BeginStumble(float back)
    {
        stumbleBack = back;
        SetPhase(Phase.Stumble, "Q_PushedOff", 0.08f);
        q.ResetMouth();
        q.SetWeakpointScale(1f);
    }

    /// <summary>Khalid's shove: stops a grab wind-up / rush (he stumbles back, a small melee opening), otherwise a nudge.</summary>
    public bool OnShoved()
    {
        if (phase == Phase.GrabWindup || phase == Phase.GrabRush)
        {
            BeginStumble(0.6f);
            return true;
        }
        q.Flinch(12f);
        if (phase == Phase.Free) MoveAgent(-q.transform.forward * 0.25f);
        return false;
    }

    // ---------------- swipe ----------------

    void StartSwipe()
    {
        swipeHits = 0;
        landedFirst = false;
        SetPhase(Phase.Swipe, "Q_Swipe", 0.1f);
        q.PlayCue(q.hissSFX);
        q.SetMouth(0.55f);
    }

    void UpdateSwipe(float dt, float d, Vector3 dir)
    {
        // lines up during the wind-up and creeps in a little, then commits
        if (phaseT < T.swipeWindup)
        {
            Face(dir, 360f);
            Vector3 step = d > 1.0f ? dir * 0.6f : Vector3.zero;
            vel = Vector3.MoveTowards(vel, step, 6f * dt);
        }
        else vel = Vector3.MoveTowards(vel, Vector3.zero, 6f * dt);
        MoveAgent(vel * dt);
        SendLocomotion(Vector3.zero);

        float hitAt = T.swipeWindup + swipeHits * T.swipeGap;
        if (swipeHits < 2 && phaseT >= hitAt)
        {
            swipeHits++;
            q.PlayCue(q.swipeSFX, 0.9f);
            TrySwipeHit();
        }
        if (phaseT >= T.swipeWindup + T.swipeGap + T.swipeRecovery) EndMove();
    }

    void TrySwipeHit()
    {
        var pc = PlayerCloseCombat.instance;
        if (pc != null && pc.IsInvulnerable) { landedFirst = false; return; }
        Vector3 toP = PlayerT.position - q.transform.position;
        toP.y = 0f;
        if (toP.magnitude > T.swipeReach) return;
        if (toP.magnitude > 0.5f && Vector3.Angle(q.transform.forward, toP) > T.swipeArc * 0.5f) return;
        if (!q.HasLineOfSightToPlayer()) return;
        // the second claw is part of the same combo: it lands even inside the i-frames the first one opened
        var hp = Player.instance.health;
        int before = hp.GetHealthValue();
        hp.Damage(T.swipeDamage, swipeHits > 1 && landedFirst);
        if (swipeHits == 1) landedFirst = true;
        if (hp.GetHealthValue() < before)
        {
            hurtPlayer = true;
            if (pc != null) pc.Knockback(toP.normalized, T.swipePushPlayer, 0.22f);
        }
    }

    // ---------------- stagger ----------------

    public void BeginStagger()
    {
        ForceReleasePlayer();
        q.ReleaseToken();
        q.SetWeakpointScale(1f);
        q.RestoreWeakpoint();            // the second mouth hit kills
        q.SetMouth(0.35f);
        q.PlayCue(q.staggerSFX, 1.1f);
        vel = Vector3.zero;
        SetPhase(Phase.StaggerDown, "Q_StaggerDown", 0.1f);
    }

    // ---------------- helpers ----------------

    void Brake(float dt)
    {
        vel = Vector3.MoveTowards(vel, Vector3.zero, 8f * dt);
        if (vel.sqrMagnitude > 0.0001f) MoveAgent(vel * dt);
        SendLocomotion(vel);
    }

    void MoveAgent(Vector3 delta)
    {
        if (q.agent.enabled && q.agent.isOnNavMesh) q.agent.Move(delta);
        else q.transform.position += delta;
    }

    void Face(Vector3 dir, float degPerSec)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
        q.transform.rotation = Quaternion.RotateTowards(q.transform.rotation, target, degPerSec * Time.deltaTime);
    }

    void SendLocomotion(Vector3 worldVel)
    {
        Vector3 local = q.transform.InverseTransformDirection(worldVel);
        q.anim.SetFloat(MoveX, local.x, 0.12f, Time.deltaTime);
        q.anim.SetFloat(MoveZ, local.z, 0.12f, Time.deltaTime);
    }
}
