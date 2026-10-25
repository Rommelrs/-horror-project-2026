using UnityEngine;

// Prototype flying enemy. Hovers above the player and drifts round them on a slow orbit: it steers a velocity toward a
// target point (never snaps), banks into its turns and bobs on Perlin noise. Every few seconds it rises and shakes (the
// tell) with its weak point out on its belly, swoops at the player and climbs back. A weak point hit knocks it out of
// the air: it drops, stays stunned a moment, then climbs back up. Built by Tools/Enemies/Build Flying Enemy Prototype.
// It is an Enemy only so the player's shots, weak point hits and gunshot alerts reach it - the ground state machine
// and NavMesh of the base class are not used (its own Update replaces the base one).
public class FlyingEnemy : Enemy
{
    [Header("Flying - hover")]
    public float detectRadius = 18f;
    [Tooltip("Height above the player's feet it hovers at.")]
    public float hoverHeight = 3.2f;
    public float orbitRadius = 3.5f;
    [Tooltip("Degrees per second it circles the player.")]
    public float orbitSpeed = 25f;
    public float maxSpeed = 5f;
    [Tooltip("How quickly its velocity turns toward where it wants to be (higher = snappier, lower = floatier).")]
    public float acceleration = 6f;
    public float bobAmount = 0.35f, bobSpeed = 0.6f;
    public float turnSpeed = 4f;
    [Tooltip("Degrees it rolls into a sideways drift.")]
    public float bankAngle = 25f;
    [Tooltip("Never lower than this above the ground under it (except when diving).")]
    public float minGroundClearance = 1.6f;

    [Header("Flying - swoop")]
    public Vector2 attackInterval = new Vector2(3.5f, 6f);
    [Tooltip("The tell: it rises and shakes, weak point out.")]
    public float telegraphTime = 1.1f;
    public float diveSpeed = 11f;
    public float hitRadius = 1.1f;
    public int damage = 15;
    public float recoverTime = 0.9f;

    [Header("Flying - weak point hit")]
    public float stunTime = 1.8f;
    public float fallGravity = 14f;
    [Tooltip("How far above the ground it stops falling when stunned (its body is ~0.5 m round).")]
    public float stunHoverHeight = 1.1f;

    [Header("Parts")]
    [Tooltip("Shaken during the tell (the colliders stay put).")]
    public Transform model;
    public Transform leftWing, rightWing;
    public float flapSpeed = 9f, flapAngle = 35f;

    enum Mode { Idle, Hover, Telegraph, Dive, Recover, Stunned, Dead }
    Mode mode = Mode.Idle;
    float modeTime, nextAttack, orbitAngle, flapPhase, noiseSeed;
    Vector3 velocity, diveTarget;
    bool hitThisDive;

    public override void Start()
    {
        health.OnDeath.AddListener(FlyingDie);
        health.OnDamageTaken += TakeDamage;
        orbitAngle = Random.value * 360f;
        noiseSeed = Random.value * 100f;
        if (rb != null) rb.isKinematic = true;
    }

    public override void OnDestroy()
    {
        health.OnDeath.RemoveListener(FlyingDie);
        health.OnDamageTaken -= TakeDamage;
    }

    void SetMode(Mode m)
    {
        mode = m; modeTime = 0f; currentState = m.ToString();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || mode == Mode.Dead) return;
        modeTime += dt;
        Flap(dt);

        var player = Player.instance;
        if (player == null || player.health == null || player.health.IsDead) { Steer(transform.position, dt); return; }
        Vector3 feet = player.transform.position;
        Vector3 chest = feet + Vector3.up * 1.3f;

        switch (mode)
        {
            case Mode.Idle:
                Steer(transform.position + Bob(), dt);
                if (Vector3.Distance(transform.position, feet) < detectRadius) StartHunting();
                break;

            case Mode.Hover:
                orbitAngle += orbitSpeed * dt;
                Steer(HoverPoint(feet) + Bob(), dt);
                Face(chest, dt);
                if (Time.time >= nextAttack && modeTime > 1f) { SetMode(Mode.Telegraph); ShowWeakpoint(true); }
                break;

            case Mode.Telegraph:
                // drifts up a little and slows to a stop over the player, shaking
                Steer(HoverPoint(feet) + Vector3.up * 0.8f, dt, 0.5f);
                Face(chest, dt * 2f);
                Shake(Mathf.Lerp(0.02f, 0.09f, modeTime / telegraphTime));
                if (modeTime >= telegraphTime)
                {
                    Shake(0f);
                    // aim a little past the player, so it swoops through rather than stopping on him
                    diveTarget = chest + (chest - transform.position).normalized * 2.5f;
                    hitThisDive = false;
                    SetMode(Mode.Dive);
                }
                break;

            case Mode.Dive:
            {
                Vector3 to = diveTarget - transform.position;
                velocity = Vector3.MoveTowards(velocity, to.normalized * diveSpeed, acceleration * 4f * dt);
                transform.position += velocity * dt;
                Face(transform.position + velocity, dt * 3f);
                if (!hitThisDive && Vector3.Distance(transform.position, chest) < hitRadius)
                {
                    hitThisDive = true;
                    player.health.Damage(damage);
                }
                if (to.magnitude < 0.6f || modeTime > 2f || Blocked(velocity, 0.6f)) { SetMode(Mode.Recover); ShowWeakpoint(false); }
                break;
            }

            case Mode.Recover:
                Steer(HoverPoint(feet), dt, 0.8f);
                Face(chest, dt);
                if (modeTime >= recoverTime) BackToHover();
                break;

            case Mode.Stunned:
            {
                // drops out of the air and stops dead about a body above the ground (easing to a stop there let the
                // fall carry it ~2 m on, through the floor), then hangs there twitching
                bool ground = GroundBelow(out float groundY);
                float stopY = ground ? groundY + stunHoverHeight : float.MinValue;
                velocity += Vector3.down * fallGravity * dt;
                velocity.x *= 1f - 2f * dt; velocity.z *= 1f - 2f * dt;
                Vector3 p = transform.position + velocity * dt;
                if (p.y <= stopY) { p.y = Mathf.Min(transform.position.y, stopY); velocity = Vector3.zero; }   // (already lower: stays)
                transform.position = p;
                Shake(0.05f);
                if (modeTime >= stunTime) { Shake(0f); BackToHover(); }
                break;
            }
        }
    }

    void StartHunting()
    {
        if (mode != Mode.Idle) return;
        playerDetected = true;
        onPlayerDetected?.Invoke();
        OnChaseStarted?.Invoke();
        BackToHover();
    }

    void BackToHover()
    {
        SetMode(Mode.Hover);
        nextAttack = Time.time + Random.Range(attackInterval.x, attackInterval.y);
    }

    // above the player, out on the orbit
    Vector3 HoverPoint(Vector3 feet)
    {
        Vector3 p = feet + Quaternion.Euler(0f, orbitAngle, 0f) * Vector3.forward * orbitRadius + Vector3.up * hoverHeight;
        if (GroundBelowPoint(p, out float ground)) p.y = Mathf.Max(p.y, ground + minGroundClearance);
        return p;
    }

    Vector3 Bob()
    {
        float t = Time.time * bobSpeed;
        return new Vector3(Mathf.PerlinNoise(noiseSeed, t) - 0.5f, (Mathf.PerlinNoise(noiseSeed + 10f, t) - 0.5f) * 1.6f, Mathf.PerlinNoise(noiseSeed + 20f, t) - 0.5f) * 2f * bobAmount;
    }

    // steer the velocity toward `target`, slowing as it arrives; slides off walls in the way
    void Steer(Vector3 target, float dt, float speedScale = 1f)
    {
        Vector3 to = target - transform.position;
        float speed = Mathf.Min(maxSpeed * speedScale, to.magnitude * 1.5f);
        Vector3 desired = to.sqrMagnitude > 0.0001f ? to.normalized * speed : Vector3.zero;
        velocity = Vector3.MoveTowards(velocity, desired, acceleration * dt);
        // (hits it already overlaps come back at distance 0 with a useless normal - flattening against that froze it)
        foreach (var hit in Physics.SphereCastAll(transform.position, 0.4f, velocity, velocity.magnitude * 0.5f + 0.3f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.distance > 0f && IsScenery(hit.collider)) { velocity = Vector3.ProjectOnPlane(velocity, hit.normal); break; }
        transform.position += velocity * dt;
    }

    // turn to look at `point`, rolled into its sideways drift
    void Face(Vector3 point, float dtTurn)
    {
        Vector3 look = point - transform.position;
        if (look.sqrMagnitude < 0.01f) return;
        float side = Vector3.Dot(velocity, transform.right);
        Quaternion want = Quaternion.LookRotation(look) * Quaternion.Euler(0f, 0f, -Mathf.Clamp(side / maxSpeed, -1f, 1f) * bankAngle);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-turnSpeed * dtTurn));
    }

    void Flap(float dt)
    {
        float speed = mode == Mode.Stunned ? flapSpeed * 2.2f : mode == Mode.Dive ? flapSpeed * 0.3f : flapSpeed;
        flapPhase += speed * dt;
        float a = Mathf.Sin(flapPhase) * (mode == Mode.Dive ? flapAngle * 0.3f : flapAngle);
        if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0f, 0f, a);
        if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0f, 0f, -a);
    }

    void Shake(float amount)
    {
        if (model != null) model.localPosition = amount > 0f ? Random.insideUnitSphere * amount : Vector3.zero;
    }

    void ShowWeakpoint(bool on)
    {
        if (enemyWeakpoint == null) return;
        enemyWeakpoint.DestorySpawnedWeakpoint();
        if (on) enemyWeakpoint.SpawnEnemyWeakpoint();
    }

    bool Blocked(Vector3 dir, float distance)
    {
        foreach (var hit in Physics.SphereCastAll(transform.position, 0.35f, dir, distance, ~0, QueryTriggerInteraction.Ignore))
            if (hit.distance > 0f && IsScenery(hit.collider)) return true;
        return false;
    }

    // walls, floors, props - not itself, the player or other enemies (it flies over and through those)
    bool IsScenery(Collider c) =>
        !c.transform.IsChildOf(transform) && c.GetComponentInParent<Player>() == null && c.GetComponentInParent<Enemy>() == null;

    bool GroundBelow(out float y) => GroundBelowPoint(transform.position, out y);

    // the nearest surface under `p` (not itself or the player)
    bool GroundBelowPoint(Vector3 p, out float y)
    {
        y = float.MinValue;
        foreach (var hit in Physics.RaycastAll(p + Vector3.up * 0.5f, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!IsScenery(hit.collider)) continue;
            y = Mathf.Max(y, hit.point.y);
        }
        return y > float.MinValue;
    }

    // ── the Enemy hooks the player's systems call ──

    public override void TakeDamage(int dmgValue)
    {
        if (health.IsDead || mode == Mode.Dead) return;
        if (stats != null) PlaySoundEffect(stats.takeDamageSFX);
        Vector3 away = transform.position - Player.instance.transform.position; away.y = 0f;
        if (health.isDamageByWeakpointHit)
        {
            // knocked out of the air
            ShowWeakpoint(false);
            Shake(0f);
            velocity = away.normalized * 3f + Vector3.up * 1.5f;
            SetMode(Mode.Stunned);
            return;
        }
        // a normal hit: a jolt away from the shot, and it notices the player
        velocity += away.normalized * 2.5f + Vector3.up * 0.5f;
        if (mode == Mode.Idle) StartHunting();
    }

    public override void AlertToSound(Vector3 soundPosition)
    {
        if (Vector3.Distance(transform.position, soundPosition) < detectRadius * 1.5f) StartHunting();
    }

    void FlyingDie()
    {
        ShowWeakpoint(false);
        Shake(0f);
        SetMode(Mode.Dead);
        // drops like a stone and tumbles
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false; rb.useGravity = true;
        rb.linearVelocity = velocity * 0.5f;
        rb.angularVelocity = Random.insideUnitSphere * 6f;
        if (Player.instance != null)
            foreach (var c in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(c, Player.instance.controller);
        OnEnemyDied?.Invoke();
        if (stats != null && stats.disableEnemyAfterDeath) Invoke(nameof(Disable), stats.releaseTime);
    }

    void Disable() => gameObject.SetActive(false);

    public override void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
}
