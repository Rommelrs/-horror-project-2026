using UnityEngine;

/// <summary>
/// Who in the horde attacks, and when (user 2026-11-20: the horde's grabs and claws felt random and unfair - "if the player
/// stands near them for 4 s, one of them does something"). One pressure meter for the whole horde:
///  - it fills while Questioners are near Khalid: in hordePressureStill (4 s) while he stands or creeps, much slower while he
///    keeps moving, and drains with none near;
///  - full: exactly ONE attacks, picked from the ones on screen (nearest the middle of the view, close by). It warns first:
///    stops, faces him, hisses, its mouth glow flares (hordeTellTime) - then the grab (still countered by a pipe swing into
///    the lunge) or the claws. A pipe hit during the warning stops it;
///  - none on screen while it's full (his back to them, camping): after hordeBehindAfter one from behind may come - claws
///    only, with a longer warning;
///  - after the attack (caught, missed, countered, hit, dead) the meter is empty and stays empty for hordeBreather.
/// Screams (the blur) are untouched: they are the story, not the damage. Ticked once a frame by the horde's combat states.
/// </summary>
public static class QuestionerHordeDirector
{
    static float pressure, breatherUntil, fullSince = -1f;
    static int tickFrame = -1;
    static QuestionerEnemy attacker, lastAttacker;
    static float attackStartedAt;
    // a safety net: no attack takes this long (a grab with its scream + bite is about 6 s)
    const float MaxAttackSeconds = 12f;

    /// <summary>0..1 - debug / tuning.</summary>
    public static float Pressure => pressure;
    public static QuestionerEnemy Attacker => attacker;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Reset();

    /// <summary>A new horde sequence: empty meter, nobody chosen.</summary>
    public static void Reset()
    {
        pressure = 0f; breatherUntil = 0f; fullSince = -1f; tickFrame = -1; attacker = lastAttacker = null;
    }

    public static void Tick(QuestionerEnemy caller)
    {
        if (tickFrame == Time.frameCount) return;
        tickFrame = Time.frameCount;
        var p = Player.instance;
        if (p == null || p.IsDead()) return;
        var T = caller.tuning;

        if (attacker != null)
        {
            if (attacker.isActiveAndEnabled && attacker.health != null && !attacker.health.IsDead
                && attacker.stateMachine.CurrentState == attacker.combatState && attacker.combatState.InHordeAttack
                && Time.time - attackStartedAt < MaxAttackSeconds) return;
            if (attacker != null && attacker.combatState != null) attacker.combatState.CancelHordeAttack();
            AttackOver(T);
        }

        if (!caller.HordeMayAttack) { pressure = 0f; return; }
        // nothing builds while he's held, on the floor, just after an attack, or loading
        if (p.isGrabbed || p.isKnockedDown || Time.time < breatherUntil || CheckpointManager.IsLoadingCheckpoint) { fullSince = -1f; return; }

        float dt = Time.deltaTime;
        Vector3 pp = p.transform.position;
        float near2 = T.hordeNearRange * T.hordeNearRange;
        bool anyNear = false;
        foreach (var o in QuestionerEnemy.All)
        {
            if (!Eligible(o)) continue;
            if (Flat(o.transform.position - pp).sqrMagnitude <= near2) { anyNear = true; break; }
        }
        if (!anyNear)
        {
            pressure = Mathf.Max(0f, pressure - dt / Mathf.Max(0.1f, T.hordePressureDrain));
            fullSince = -1f;
            return;
        }
        Vector3 v = p.controller != null ? Flat(p.controller.velocity) : Vector3.zero;
        bool moving = v.magnitude >= T.hordeMovingSpeed;
        pressure = Mathf.Min(1f, pressure + dt / Mathf.Max(0.1f, moving ? T.hordePressureMoving : T.hordePressureStill));
        if (pressure < 1f) { fullSince = -1f; return; }
        if (fullSince < 0f) fullSince = Time.time;

        var pick = Pick(p, T, true);
        bool behind = false;
        if (pick == null && Time.time - fullSince >= T.hordeBehindAfter) { pick = Pick(p, T, false); behind = pick != null; }
        if (pick == null) return;
        if (pick.combatState.BeginHordeAttack(behind ? T.hordeBehindTellTime : T.hordeTellTime, !behind))
        {
            attacker = lastAttacker = pick;
            attackStartedAt = Time.time;
            pressure = 0f;
            fullSince = -1f;
        }
    }

    static void AttackOver(QuestionerTuning T)
    {
        attacker = null;
        pressure = 0f;
        fullSince = -1f;
        breatherUntil = Time.time + T.hordeBreather;
    }

    static bool Eligible(QuestionerEnemy o) =>
        o != null && o.hordeMode && o.isActiveAndEnabled && o.health != null && !o.health.IsDead && !o.InLineUp
        && o.stateMachine.CurrentState == o.combatState;

    /// <summary>The one to attack: free to start a move, close enough, in sight - on screen (nearest the middle of the view and
    /// close) or, with onScreen false, anyone (the nearest).</summary>
    static QuestionerEnemy Pick(Player p, QuestionerTuning T, bool onScreen)
    {
        var cam = Camera.main;
        Vector3 pp = p.transform.position;
        float reach2 = T.hordeGrabMax * T.hordeGrabMax;
        QuestionerEnemy best = null;
        float bestScore = float.MaxValue;
        foreach (var o in QuestionerEnemy.All)
        {
            if (!Eligible(o) || !o.combatState.CanStartHordeAttack) continue;
            float d2 = Flat(o.transform.position - pp).sqrMagnitude;
            if (d2 > reach2) continue;
            float score = Mathf.Sqrt(d2) * 0.15f;
            if (o == lastAttacker) score += 0.6f;          // someone else next time, when there is anyone else
            if (onScreen)
            {
                if (cam == null) return null;
                Vector3 vp = cam.WorldToViewportPoint(o.transform.position + Vector3.up * 1.6f);
                if (vp.z < 0.5f || vp.x < 0.08f || vp.x > 0.92f || vp.y < 0.05f || vp.y > 0.98f) continue;
                score += Mathf.Abs(vp.x - 0.5f);
            }
            if (score >= bestScore || !o.HasLineOfSightToPlayer()) continue;
            best = o;
            bestScore = score;
        }
        return best;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
