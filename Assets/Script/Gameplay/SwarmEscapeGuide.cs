using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Localization;

/// <summary>
/// Block 2 Questioner swarm (duct tape): the cues that make the way out readable (user 2026-11-20).
///  - The tape cutscene (user's beats 2026-11-20; v2 after "the animation, the camera, the direction, the angles are bad"):
///      1. FRONT, wide, still: Khalid has the tape, turns from the wardrobe and walks back up the road, towards the camera.
///      2. The glass breaks (the swarm starts): he flinches mid-step (mocap "Reacting Surprised") and the SAME camera
///         crash-zooms onto his face; it holds there, hand-held, while he stands frozen and shaking.
///      3. CUTAWAY: low in the road in front of the horde standing in the sandstorm - their mouths light up one after another.
///      4. OVER HIS SHOULDER, long lens, still: the row of lit mouths ahead in the storm, him frozen in front of them:
///         "I have to get outta here!" - he starts to back away (mocap "Walk Backwards Fear", its steps move him for
///         real) and the game camera eases back in.
///    BeginIntro = spawner onItemDetected (the moment he has it), SwarmStarts = spawner onItemPickedUp (the glass).
///  - OnDoorLocked: the west door locks with a sound and a line (LockdoorTrigger onLocked).
///  - Collapse: the escape zone went off. The user's own blocks do the collapse (the zone switches BlocksToDestroy off and
///    their rubble on - untouched here); this adds the sound, the camera glance at the alley entrance, and Adel calling
///    from the exit, 3D, again and again until Khalid gets there.
/// The way being open is a story flag, so a Continue after it keeps it open.
/// </summary>
public class SwarmEscapeGuide : MonoBehaviour
{
    public const string LaneOpenFlag = "Block2SwarmLaneOpen";

    [Tooltip("Mixer group for Adel's calls (SFX).")]
    [SerializeField] AudioMixerGroup sfxGroup;

    [Header("Tape cutscene - Khalid")]
    [Tooltip("Where the Questioners line up: he turns to it to go back, and faces it at the end.")]
    [SerializeField] Transform questionersAt;
    [Tooltip("He walks back this fast (m/s) until the glass breaks.")]
    [SerializeField] float walkBackSpeed = 1.1f;
    [Tooltip("About how far he gets before the glass (m): the front camera is set up for him stopping there. (Short on "
           + "purpose: a few steps further and he stands right beside the fallen wardrobe, which then fills the last shot.)")]
    [SerializeField] float expectedWalk = 1.7f;
    [Tooltip("Never walks further than this (m).")]
    [SerializeField] float walkBackMax = 2.6f;
    [Tooltip("Before he turns (s), after the inspect screen closes: a breath with the tape in his hand.")]
    [SerializeField] float beforeTurn = 0.7f;
    [Tooltip("Animator state (Movement layer) for the flinch and the frozen stance after it: Khalid_Startled.")]
    [SerializeField] string startledState = "SwarmStartled";
    [Tooltip("The flinch is this far into that clip (s): the clip starts here when the glass breaks.")]
    [SerializeField] float startledFrom = 0.22f;
    [Tooltip("Animator state for backing away at the end: Khalid_BackAwayFear.")]
    [SerializeField] string backAwayState = "SwarmBackAway";
    [Tooltip("How fast the backing-away clip plays (1 = about 0.5 m a second).")]
    [SerializeField] float backAwaySpeed = 1f;
    [Tooltip("How hard he shakes (1 = about a centimetre at his head).")]
    [SerializeField] float tremble = 1f;

    [Header("Tape cutscene - shot 1+2: front, then the zoom on his face")]
    [Tooltip("The camera stands this far up the road from where he will stop (m)...")]
    [SerializeField] float frontDistance = 3.4f;
    [Tooltip("...this far to his right...")]
    [SerializeField] float frontSide = 0.95f;
    [Tooltip("...and this high above the ground.")]
    [SerializeField] float frontHeight = 1.45f;
    [SerializeField] float wideFov = 40f;
    [Tooltip("The close-up frames this much height at his face (m): the zoom's lens is worked out from it.")]
    [SerializeField] float closeUpFrame = 0.74f;
    [Tooltip("The zoom takes this long (s).")]
    [SerializeField] float zoomSeconds = 0.32f;
    [Tooltip("From the glass to the cut away (s).")]
    [SerializeField] float closeUpSeconds = 2.8f;
    [Tooltip("A soft warm light on his face for the close-up (0 = none): his face is in shadow under the sandstorm sky.")]
    [SerializeField] float faceFill = 1.2f;

    [Header("Tape cutscene - shot 3: the horde in the storm")]
    [Tooltip("The camera is this far in front of the line-up (m)...")]
    [SerializeField] float hordeDistance = 5.2f;
    [Tooltip("...and this high above the ground (low: they tower).")]
    [SerializeField] float hordeHeight = 0.6f;
    [SerializeField] float hordeFov = 46f;
    [SerializeField] float hordeSeconds = 2.5f;
    [Tooltip("Their mouths light up over this long (s), the nearest first.")]
    [SerializeField] float igniteSeconds = 1.5f;

    [Header("Tape cutscene - shot 4: over his shoulder")]
    [SerializeField] float shoulderBehind = 2.4f;
    [SerializeField] float shoulderSide = 0.75f;
    [SerializeField] float shoulderHeight = 1.6f;
    [SerializeField] float shoulderFov = 26f;
    [SerializeField] float shoulderSeconds = 3.0f;
    [Tooltip("He stares from this far through the startled clip (0-1): facing ahead again, standing still.")]
    [SerializeField] float stareFrom = 0.8f;
    [Tooltip("He backs away for the last this long of the shot (s).")]
    [SerializeField] float stepBackSeconds = 0.9f;
    [Tooltip("The line comes this long into the shot (s).")]
    [SerializeField] float lineAt = 0.9f;
    [Tooltip("The line at the end: \"I have to get outta here!\"")]
    [SerializeField] LocalizedString goalLine;
    [SerializeField] float goalLineSeconds = 3f;
    [Tooltip("No Questioner can hurt him for this long after the cutscene (s).")]
    [SerializeField] float introGrace = 2f;

    [Header("West door locks")]
    [SerializeField] Transform lockedDoor;
    [SerializeField] AudioClip lockSound;
    [SerializeField] float lockVolume = 1f;
    [SerializeField] LocalizedString lockedLine;
    [SerializeField] float lockedLineDelay = 0.6f;
    [SerializeField] float lockedLineSeconds = 2.8f;

    [Header("The collapse (the blocks themselves are the user's: the zone swaps them)")]
    [SerializeField] AudioClip collapseSound;
    [SerializeField] float collapseVolume = 1f;
    [Tooltip("Where the collapse sound plays (the gap). Empty = here.")]
    [SerializeField] Transform collapseAt;
    [Tooltip("Also done when the way is already open on load.")]
    [SerializeField] GameObject[] activateWhenOpen;
    [SerializeField] GameObject[] deactivateWhenOpen;

    [Header("Camera glance at the alley entrance")]
    [Tooltip("What the camera (and Khalid) turn to: the entrance of the alley that leads to the way out.")]
    [SerializeField] Transform glanceAt;
    [SerializeField] float glanceDelay = 0.35f;
    [SerializeField] float glanceSeconds = 2.2f;
    [SerializeField] float immunitySeconds = 3.5f;

    [Header("Adel calls from the exit")]
    [SerializeField] Transform exitPoint;
    [SerializeField] AudioClip callClip;
    [SerializeField] float callVolume = 1f;
    [SerializeField] float firstCallDelay = 1.2f;
    [SerializeField] float callEvery = 7f;
    [SerializeField] float callMaxDistance = 70f;
    [SerializeField] LocalizedString callLine;
    [SerializeField] float callLineSeconds = 2.5f;
    [Tooltip("The subtitle goes with the first this many calls (the voice keeps calling after).")]
    [SerializeField] int callLineTimes = 2;
    [Tooltip("Khalid this close to the exit: Adel stops calling.")]
    [SerializeField] float arrivedDistance = 9f;

    bool collapsed, introRunning, swarmStarted;
    AudioSource callSource;
    float callLineUntil = -1f;
    CinemachineVirtualCamera shot;
    CinemachineBrain brain;
    CinemachineBlendDefinition savedBlend;
    bool blendSaved;

    // Khalid held by the cutscene
    readonly List<Behaviour> disabled = new List<Behaviour>();
    bool pausedMovement, madeInvincible;

    // Khalid's model while the cutscene acts on it (LateUpdate): the shaking, the flinch, and the mocap's own steps
    Transform model, rootBone;
    Vector3 modelBasePos, rootRest, lastRootOff;
    Quaternion modelBaseRot;
    bool modelActing, rootTracking, rootDriving;
    float trembleNow, flinchAt = -10f;

    /// <summary>Read-only, for tests.</summary>
    public bool IntroRunning => introRunning;
    /// <summary>Read-only, for tests: 1 front, 2 the zoom, 3 the horde, 4 over his shoulder (0 = none).</summary>
    public int IntroShot { get; private set; }

    void Start()
    {
        if (StoryFlags.Has(LaneOpenFlag)) OpenSilently();
    }

    // ------------------------------------------------------------------ the tape cutscene

    /// <summary>He has the tape (spawner onItemDetected): the cutscene starts once the inspect screen is closed.</summary>
    public void BeginIntro()
    {
        if (introRunning) return;
        swarmStarted = false;
        StartCoroutine(Co_Intro());
    }

    /// <summary>The glass breaks and the swarm starts (spawner onItemPickedUp).</summary>
    public void SwarmStarts()
    {
        swarmStarted = true;
        if (!introRunning) Say(goalLine, goalLineSeconds);   // no cutscene ran (e.g. a test): the line at least
    }

    IEnumerator Co_Intro()
    {
        introRunning = true;
        while (GameManager.IsPaused) yield return null;      // the inspect screen
        var player = Player.instance;
        if (player == null || player.IsDead()) { introRunning = false; yield break; }
        Hold(player);
        PlayerMeleeWeapon.SetCutscene(PlayerMeleeWeapon.CutsceneHold.Away);
        var pm = player.GetComponent<PlayerMovement>();
        var anim = player.animator;
        BeginActing(player);

        Vector3 k0 = player.transform.position;
        Vector3 back = questionersAt != null ? Flat(questionersAt.position - k0) : -player.transform.forward;
        if (back.sqrMagnitude < 0.01f) back = -player.transform.forward;
        back.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, back);          // his right once he faces them
        Quaternion faceBack = Quaternion.LookRotation(back);

        // ---------------------------------------------------------------- 1. front, wide: he turns and walks back
        IntroShot = 1;
        BeginCamera();
        Cut();
        Vector3 mark = k0 + back * expectedWalk;                 // about where the glass will stop him
        Vector3 camA = ClearSpot(mark + Vector3.up * 1.5f, mark + back * frontDistance + side * frontSide + Vector3.up * frontHeight);
        Vector3 aim = k0 + Vector3.up * 1.2f;
        float t = 0f, walked = 0f;
        bool first = true;
        while (!swarmStarted && t < 8f)
        {
            float dt = Time.deltaTime;
            if (t >= beforeTurn)
            {
                // he comes round in an arc: turning on the spot first, stepping off as he faces the way
                player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, faceBack, 230f * dt);
                float off = Quaternion.Angle(player.transform.rotation, faceBack);
                float go = walked < walkBackMax ? Mathf.InverseLerp(100f, 25f, off) : 0f;
                if (go > 0f)
                {
                    Vector3 step = player.transform.forward * walkBackSpeed * go * dt;
                    if (player.controller != null && player.controller.enabled) player.controller.Move(step + Vector3.down * 2f * dt);
                    else player.transform.position += step;
                    walked = Flat(player.transform.position - k0).magnitude;
                }
                Anim(player, pm, go);
            }
            else Anim(player, pm, 0f);
            aim = Vector3.Lerp(aim, player.transform.position + Vector3.up * 1.2f, 1f - Mathf.Exp(-5f * dt));
            Place(camA, aim + Handheld(camA, aim, wideFov, 0.5f), Mathf.Lerp(wideFov + 3f, wideFov, t / 3f), first);
            first = false;
            t += dt;
            yield return null;
        }

        // ---------------------------------------------------------------- 2. the glass: the flinch, the zoom, the shaking
        IntroShot = 2;
        Anim(player, pm, 0f);
        if (anim != null && anim.HasState(0, Animator.StringToHash(startledState)))
            anim.CrossFadeInFixedTime(startledState, 0.08f, 0, startledFrom);
        flinchAt = Time.time;
        trembleNow = tremble;
        Transform headBone = FindBone(model != null ? model : player.transform, "head.x");
        Vector3 face = HeadPoint(player, headBone);
        float fromFov = wideFov;
        float dist = Vector3.Distance(camA, face);
        float closeFov = Mathf.Clamp(2f * Mathf.Atan(closeUpFrame * 0.5f / Mathf.Max(0.5f, dist)) * Mathf.Rad2Deg, 6f, 30f);
        Vector3 fromAim = aim;
        Light fill = null;
        if (faceFill > 0f)
        {
            fill = new GameObject("Swarm cutscene face light").AddComponent<Light>();
            fill.transform.SetParent(transform, false);
            fill.type = LightType.Spot; fill.spotAngle = 30f; fill.range = dist + 3f; fill.shadows = LightShadows.None;
            fill.color = new Color(1f, 0.9f, 0.75f); fill.intensity = 0f;
            fill.transform.position = camA + side * 0.9f + Vector3.up * 0.5f;      // beside the camera, a little above
        }
        for (t = 0f; t < closeUpSeconds; t += Time.deltaTime)
        {
            if (fill != null) { fill.intensity = faceFill * EaseOut(t / zoomSeconds); fill.transform.LookAt(face); }
            player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, faceBack, 300f * Time.deltaTime);
            face = Vector3.Lerp(face, HeadPoint(player, headBone), 1f - Mathf.Exp(-6f * Time.deltaTime));
            float z = EaseOut(t / zoomSeconds);
            // the zoom lands a touch past the framing and settles; then it creeps in for as long as it holds
            float fov = Mathf.Lerp(fromFov, closeFov, z) - Mathf.Sin(Mathf.Clamp01(t / (zoomSeconds * 2.2f)) * Mathf.PI) * 0.9f
                        - closeFov * 0.12f * Mathf.Clamp01((t - zoomSeconds) / closeUpSeconds);
            Vector3 look = Vector3.Lerp(fromAim, face, z);
            float jolt = Mathf.Exp(-t * 6f) * 2.2f;              // the hit of the glass on the camera
            Place(camA, look + Handheld(camA, look, fov, 1f + jolt), fov, false);
            yield return null;
        }

        if (fill != null) Destroy(fill.gameObject);

        // ---------------------------------------------------------------- 3. the horde in the storm, mouths lighting up
        var horde = new List<QuestionerEnemy>();
        Vector3 centre = Vector3.zero;
        int inLine = 0;
        foreach (var q in QuestionerEnemy.All)
        {
            if (q == null || !q.hordeMode || !q.isActiveAndEnabled || q.health == null || q.health.IsDead) continue;
            horde.Add(q);
            if (q.InLineUp) { centre += q.transform.position; inLine++; }
        }
        Vector3 k1 = player.transform.position;
        Vector3 farPoint = k1 + back * 15f;
        if (inLine >= 4)
        {
            IntroShot = 3;
            centre /= inLine;
            farPoint = centre;
            Vector3 toK = Flat(k1 - centre).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, toK);
            Vector3 lowFrom = centre + toK * hordeDistance - across * 1.1f + Vector3.up * hordeHeight;
            Vector3 lowTo = centre + toK * (hordeDistance - 0.9f) + across * 0.5f + Vector3.up * (hordeHeight + 0.1f);
            // nearest the camera first
            horde.Sort((a, b) => (a.transform.position - lowFrom).sqrMagnitude.CompareTo((b.transform.position - lowFrom).sqrMagnitude));
            if (anim != null && anim.HasState(0, Animator.StringToHash(backAwayState))) anim.CrossFadeInFixedTime(backAwayState, 0.3f, 0, 0f);
            for (t = 0f; t < hordeSeconds; t += Time.deltaTime)
            {
                Ignite(horde, t);
                float k = Mathf.SmoothStep(0f, 1f, t / hordeSeconds);
                Vector3 pos = Vector3.Lerp(lowFrom, lowTo, k);
                Vector3 look = centre + Vector3.up * 1.75f;
                Place(pos, look + Handheld(pos, look, hordeFov, 0.6f), hordeFov, false);
                yield return null;
            }
        }
        float igniteFrom = Time.time - (inLine >= 4 ? hordeSeconds : 0f);

        // ---------------------------------------------------------------- 4. over his shoulder: the lights ahead; he steps back
        IntroShot = 4;
        player.transform.rotation = faceBack;
        float animSpeed = anim != null ? anim.speed : 1f;
        if (anim != null && anim.HasState(0, Animator.StringToHash(startledState)))
        {
            // frozen, staring at them: the end of the startled clip (he faces ahead again there), slowed right down
            anim.Play(startledState, 0, stareFrom);
            anim.speed = 0.3f;
        }
        trembleNow = tremble * 0.8f;
        // still, long lens, from behind his right shoulder: he fills the left of the frame (and hides the fallen wardrobe
        // beside him), the lights fill the rest. A camera that travelled back with him brought that wardrobe in as a slab.
        Vector3 camB = ClearSpot(k1 + Vector3.up * 1.5f, k1 - back * shoulderBehind + side * shoulderSide + Vector3.up * shoulderHeight);
        Vector3 lightsAt = new Vector3(farPoint.x, k1.y + 1.45f, farPoint.z);
        bool said = false, stepping = false;
        for (t = 0f; t < shoulderSeconds; t += Time.deltaTime)
        {
            Ignite(horde, Time.time - igniteFrom);
            if (!said && t >= lineAt) { said = true; Say(goalLine, goalLineSeconds); }
            if (!stepping && t >= shoulderSeconds - stepBackSeconds)
            {
                // he starts to back away from them: the game takes over as he goes
                stepping = true;
                if (anim != null)
                {
                    anim.speed = backAwaySpeed;
                    if (anim.HasState(0, Animator.StringToHash(backAwayState))) anim.CrossFadeInFixedTime(backAwayState, 0.2f, 0, 0.1f);
                }
                BeginRootSteps();
            }
            float k = Mathf.SmoothStep(0f, 1f, t / shoulderSeconds);
            Place(camB, lightsAt + Handheld(camB, lightsAt, shoulderFov, 0.8f), shoulderFov - 2.5f * k, false);
            yield return null;
        }

        // ---------------------------------------------------------------- back to the game
        if (anim != null) anim.speed = animSpeed;
        rootDriving = false;
        trembleNow = 0f;
        if (anim != null) anim.CrossFadeInFixedTime("WalkMove", 0.45f, 0);
        player.transform.rotation = faceBack;
        if (CameraSystem.Instance != null) CameraSystem.Instance.SnapBehind();
        EndCamera(0.8f);
        yield return Wait(0.5f);
        EndActing();
        PlayerMeleeWeapon.SetCutscene(PlayerMeleeWeapon.CutsceneHold.None);
        Release(player);
        if (player.health != null) player.health.GrantInvulnerability(introGrace);
        IntroShot = 0;
        introRunning = false;
        // their mouths die down again, slowly: he still sees them ahead as the game goes on
        for (t = 0f; t < 3f; t += Time.deltaTime)
        {
            foreach (var q in horde) if (q != null && q.InLineUp) q.SetGlowFlare(1f - t / 3f);
            yield return null;
        }
        foreach (var q in horde) if (q != null && q.InLineUp) q.SetGlowFlare(0f);
    }

    /// <summary>The horde's mouths light up, one after another in the order of the list, over igniteSeconds.</summary>
    void Ignite(List<QuestionerEnemy> horde, float t)
    {
        int n = horde.Count;
        for (int i = 0; i < n; i++)
        {
            var q = horde[i];
            if (q == null || !q.InLineUp) continue;              // only the ones standing: the rest are in the fight's hands
            float at = n > 1 ? igniteSeconds * 0.75f * i / (n - 1f) : 0f;
            q.SetGlowFlare(Mathf.Clamp01((t - at) / 0.4f));
        }
    }

    static float EaseOut(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - Mathf.Pow(1f - x, 4f);
    }

    /// <summary>The middle of his face (his head bone sits at the base of the skull).</summary>
    static Vector3 HeadPoint(Player player, Transform headBone) =>
        headBone != null ? headBone.position + Vector3.up * 0.09f : player.transform.position + Vector3.up * 1.72f;

    /// <summary>A hand-held camera: a small drift of the point it looks at, the same size on screen whatever the lens.</summary>
    static Vector3 Handheld(Vector3 pos, Vector3 look, float fov, float amount)
    {
        float d = Vector3.Distance(pos, look);
        float size = d * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 0.022f * amount;      // ~1% of the frame
        float tt = Time.time;
        float x = (Mathf.PerlinNoise(tt * 0.9f, 3.1f) - 0.5f) * 2f + (Mathf.PerlinNoise(tt * 4.3f, 7.7f) - 0.5f) * 0.7f;
        float y = (Mathf.PerlinNoise(5.3f, tt * 1.1f) - 0.5f) * 2f + (Mathf.PerlinNoise(9.1f, tt * 3.7f) - 0.5f) * 0.7f;
        Vector3 fwd = (look - pos).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
        Vector3 up = Vector3.Cross(fwd, right);
        return (right * x + up * y) * size;
    }

    /// <summary>'want', or as far towards it from 'from' as the way is clear (a camera never inside a wardrobe).</summary>
    static Vector3 ClearSpot(Vector3 from, Vector3 want)
    {
        Vector3 d = want - from;
        float len = d.magnitude;
        if (len < 0.01f) return want;
        float best = len;
        foreach (var h in Physics.SphereCastAll(from, 0.25f, d / len, len, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider == null || h.collider.GetComponentInParent<Player>() != null || h.collider.GetComponentInParent<Enemy>() != null) continue;
            if (h.distance > 0.01f && h.distance < best) best = h.distance;
        }
        return best >= len ? want : from + d / len * Mathf.Max(0.6f, best - 0.3f);
    }

    void Cut()
    {
        if (brain != null) brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);
    }

    static Transform FindBone(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static void Anim(Player p, PlayerMovement pm, float y)
    {
        if (pm != null) pm.SetExternalMoveInput(new Vector2(0f, y));
        if (p.animator != null) { p.animator.SetFloat("x", 0f); p.animator.SetFloat("y", y); }
    }

    // ---------------- Khalid's model: the shaking, the flinch, the mocap's steps (after the animator, every frame)

    void BeginActing(Player player)
    {
        model = player.animator != null ? player.animator.transform : null;
        if (model == null || model == player.transform) { model = null; return; }
        modelBasePos = model.localPosition;
        modelBaseRot = model.localRotation;
        rootBone = FindBone(model, "c_root_master.x");
        rootTracking = rootDriving = false;
        trembleNow = 0f;
        flinchAt = -10f;
        modelActing = true;
    }

    /// <summary>From now on the clip's own travel (his hips) moves Khalid himself, so his feet don't slide and he is
    /// where he looks to be when the game takes over.</summary>
    void BeginRootSteps()
    {
        if (model == null || rootBone == null) return;
        // the clip's first pose is the reference (the animator has not posed it yet this frame: taken on the next LateUpdate)
        rootTracking = true;
        rootDriving = true;
        rootRestPending = true;
        lastRootOff = Vector3.zero;
    }
    bool rootRestPending;

    void EndActing()
    {
        modelActing = rootTracking = rootDriving = false;
        if (model != null) { model.localPosition = modelBasePos; model.localRotation = modelBaseRot; }
        model = null;
    }

    void LateUpdate()
    {
        if (!modelActing || model == null) return;
        Vector3 pos = modelBasePos;
        Quaternion rot = modelBaseRot;

        if (rootTracking && rootBone != null)
        {
            Vector3 local = model.InverseTransformPoint(rootBone.position);
            if (rootRestPending) { rootRest = local; rootRestPending = false; }
            Vector3 off = local - rootRest; off.y = 0f;
            Vector3 inPlayer = modelBaseRot * off;               // model space -> Khalid's
            var player = Player.instance;
            if (rootDriving && player != null)
            {
                Vector3 step = player.transform.TransformVector(inPlayer - lastRootOff);
                if (player.controller != null && player.controller.enabled) player.controller.Move(step + Vector3.down * 2f * Time.deltaTime);
                else player.transform.position += step;
                lastRootOff = inPlayer;
            }
            // what his steps have not moved him by, the model gives back (so nothing shows twice, and nothing snaps after)
            pos -= inPlayer;
        }

        float tt = Time.time;
        if (trembleNow > 0f)
        {
            // a shiver from the feet up, with a slower sway under it
            float a = trembleNow;
            float pitch = (Mathf.PerlinNoise(tt * 14f, 1.3f) - 0.5f) * 0.5f + (Mathf.PerlinNoise(tt * 1.3f, 4.2f) - 0.5f) * 0.6f;
            float roll = (Mathf.PerlinNoise(3.7f, tt * 12f) - 0.5f) * 0.5f + (Mathf.PerlinNoise(8.8f, tt * 1.1f) - 0.5f) * 0.5f;
            rot = rot * Quaternion.Euler(pitch * a, 0f, roll * a);
            pos += new Vector3((Mathf.PerlinNoise(tt * 17f, 9f) - 0.5f), 0f, (Mathf.PerlinNoise(11f, tt * 16f) - 0.5f)) * 0.006f * a;
        }
        float f = tt - flinchAt;
        if (f >= 0f && f < 1.2f)
        {
            // the flinch: he ducks and jerks back, then comes up slowly
            float e = f < 0.07f ? f / 0.07f : Mathf.Exp(-(f - 0.07f) * 4.5f);
            pos += Vector3.down * 0.05f * e;
            rot = rot * Quaternion.Euler(4.5f * e, 0f, 0f);
        }
        model.localPosition = pos;
        model.localRotation = rot;
    }

    void Hold(Player player)
    {
        if (!player.pauseMovement) { player.pauseMovement = true; pausedMovement = true; }
        var pm = player.GetComponent<PlayerMovement>();
        if (pm != null) pm.SetExternalMoveInput(Vector2.zero);
        Disable(player.playerWeaponSystem);
        Disable(player.GetComponent<PlayerRoll>());
        Disable(player.GetComponent<PlayerMeleeAttack>());
        Disable(player.GetComponent<DoorInteractionHandler>());
        if (player.health != null && !player.health.Invincible) { player.health.Invincible = true; madeInvincible = true; }
        if (player.animator != null) player.animator.SetBool("Turning", false);
    }

    void Disable(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false; disabled.Add(b);
    }

    void Release(Player player)
    {
        if (player != null)
        {
            if (pausedMovement) player.pauseMovement = false;
            if (madeInvincible && player.health != null) player.health.Invincible = false;
            var pm = player.GetComponent<PlayerMovement>();
            if (pm != null) pm.SetExternalMoveInput(Vector2.zero);
        }
        pausedMovement = false; madeInvincible = false;
        foreach (var b in disabled) if (b != null) b.enabled = true;
        disabled.Clear();
    }

    // ------------------------------------------------------------------ west door

    [Tooltip("The swarm can't be pushed aside while he is within this distance (m) of the locked door.")]
    [SerializeField] float solidSwarmDistance = 10f;
    static SwarmEscapeGuide atLockedDoor;

    /// <summary>He has found the west door locked and is still at it: the swarm can't be pushed aside (only there - user 2026-11-25).</summary>
    public static bool SwarmHoldsGround => atLockedDoor != null && !atLockedDoor.collapsed && atLockedDoor.lockedDoor != null && Player.instance != null
        && Vector3.Distance(Player.instance.transform.position, atLockedDoor.lockedDoor.position) < atLockedDoor.solidSwarmDistance;

    public void OnDoorLocked()
    {
        atLockedDoor = this;
        Vector3 at = lockedDoor != null ? lockedDoor.position : transform.position;
        if (lockSound != null) PlayAt(lockSound, at, lockVolume, 40f);
        StartCoroutine(Co_Later(lockedLineDelay, () => Say(lockedLine, lockedLineSeconds)));
    }

    // ------------------------------------------------------------------ the collapse

    public void Collapse()
    {
        if (collapsed) return;
        collapsed = true;
        StoryFlags.Set(LaneOpenFlag);
        SetOpenObjects();

        Vector3 soundAt = collapseAt != null ? collapseAt.position : transform.position;
        if (collapseSound != null) PlayAt(collapseSound, soundAt, collapseVolume, 70f);

        var player = Player.instance;
        if (player != null && player.health != null) player.health.GrantInvulnerability(immunitySeconds);
        Vector3 look = glanceAt != null ? glanceAt.position : soundAt + Vector3.up * 0.3f;
        StartCoroutine(Co_Glance(look));
        StartCoroutine(Co_AdelCalls());
    }

    /// <summary>Loaded with the way already open: no sound, no glance (and no calls: he's been told).</summary>
    void OpenSilently()
    {
        collapsed = true;
        SetOpenObjects();
    }

    void SetOpenObjects()
    {
        if (activateWhenOpen != null) foreach (var g in activateWhenOpen) if (g != null) g.SetActive(true);
        if (deactivateWhenOpen != null) foreach (var g in deactivateWhenOpen) if (g != null) g.SetActive(false);
    }

    // ------------------------------------------------------------------ the glance (the game camera, turned)

    IEnumerator Co_Glance(Vector3 look)
    {
        yield return new WaitForSeconds(glanceDelay);
        var player = Player.instance;
        var cam = Camera.main;
        if (player == null || cam == null) yield break;
        if (cam.GetComponent<CinemachineBrain>() == null) yield break;

        // the game camera exactly as it is (spot behind Khalid, lens): only its direction changes
        Vector3 offset = cam.transform.position - player.transform.position;
        BeginCamera();
        bool paused = !player.pauseMovement;
        if (paused) player.pauseMovement = true;
        // Khalid turns to it too
        Vector3 face = look - player.transform.position; face.y = 0f;

        float end = Time.time + glanceSeconds;
        bool first = true;
        while (Time.time < end && player != null)
        {
            Vector3 pos = player.transform.position + offset;
            Place(pos, look, cam.fieldOfView, first, cam.farClipPlane, cam.nearClipPlane, true);
            first = false;
            if (face.sqrMagnitude > 0.01f)
                player.transform.rotation = Quaternion.Slerp(player.transform.rotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-6f * Time.deltaTime));
            yield return null;
        }
        // the game camera comes back behind him, facing the alley too (not round to where it looked before)
        if (player != null && face.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.LookRotation(face);
        if (CameraSystem.Instance != null) CameraSystem.Instance.SnapBehind();
        if (paused && player != null) player.pauseMovement = false;
        EndCamera(0.7f);
    }

    // ------------------------------------------------------------------ the cutscene camera (one vcam, eased in and out)

    void BeginCamera()
    {
        var cam = Camera.main;
        brain = cam != null ? cam.GetComponent<CinemachineBrain>() : null;
        if (brain != null && !blendSaved) { savedBlend = brain.m_DefaultBlend; blendSaved = true; }
        if (brain != null) brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.7f);
        if (shot == null)
        {
            var go = new GameObject("Swarm cutscene camera");
            go.transform.SetParent(transform, false);
            shot = go.AddComponent<CinemachineVirtualCamera>();
            shot.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
            shot.Priority = 0;
        }
    }

    void Place(Vector3 pos, Vector3 target, float fov, bool first, float far = 25f, float near = 0.08f, bool keepLens = false)
    {
        if (shot == null) return;
        shot.m_Lens.FieldOfView = fov;
        shot.m_Lens.FarClipPlane = far;
        shot.m_Lens.NearClipPlane = keepLens ? near : 0.08f;
        shot.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos, Vector3.up));
        if (first) shot.Priority = 70;
    }

    void EndCamera(float blend)
    {
        if (brain != null) brain.m_DefaultBlend = blend > 0f ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, blend)
                                                             : new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);
        if (shot != null) shot.Priority = 0;
        StartCoroutine(Co_RestoreBlend(blend + 0.2f));
    }

    IEnumerator Co_RestoreBlend(float after)
    {
        yield return new WaitForSeconds(after);
        if (brain != null && blendSaved && (shot == null || shot.Priority == 0)) { brain.m_DefaultBlend = savedBlend; blendSaved = false; }
    }

    // ------------------------------------------------------------------ Adel

    IEnumerator Co_AdelCalls()
    {
        if (exitPoint == null || callClip == null) yield break;
        callSource = new GameObject("Adel calling (3D)").AddComponent<AudioSource>();
        callSource.transform.SetParent(transform, false);
        callSource.transform.position = exitPoint.position;
        callSource.playOnAwake = false;
        callSource.spatialBlend = 1f;
        callSource.rolloffMode = AudioRolloffMode.Linear;
        callSource.minDistance = 6f;
        callSource.maxDistance = callMaxDistance;
        callSource.dopplerLevel = 0f;
        callSource.volume = callVolume;
        callSource.outputAudioMixerGroup = sfxGroup;

        yield return new WaitForSeconds(firstCallDelay);
        int calls = 0;
        while (true)
        {
            var player = Player.instance;
            if (player == null || player.health == null || player.health.IsDead) yield break;
            if (Vector3.Distance(player.transform.position, exitPoint.position) <= arrivedDistance) yield break;
            if (!PlayerMeleeWeapon.InCutscene)
            {
                callSource.PlayOneShot(callClip);
                if (calls < callLineTimes) { Say(callLine, callLineSeconds); callLineUntil = Time.time + callLineSeconds + 1f; }
                calls++;
            }
            float next = Time.time + callEvery;
            while (Time.time < next)
            {
                player = Player.instance;
                if (player != null && Vector3.Distance(player.transform.position, exitPoint.position) <= arrivedDistance)
                {
                    // he's there: the line goes (it stayed up over the exit's own lines)
                    if (Time.time < callLineUntil && SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) SubtitleManager.instance.ForceClose();
                    yield break;
                }
                yield return null;
            }
        }
    }

    // ------------------------------------------------------------------ helpers

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static IEnumerator Wait(float s) { for (float e = 0f; e < s; e += Time.deltaTime) yield return null; }

    static void Say(LocalizedString line, float seconds)
    {
        if (line == null || line.IsEmpty) return;
        SubtitleManager.ShowSubtitle(line, false, null, seconds);
    }

    void PlayAt(AudioClip clip, Vector3 at, float volume, float maxDistance)
    {
        if (SoundEffectManager.instance != null) { SoundEffectManager.instance.PlaySFXAtPosition(clip, at, volume, false, 8f, maxDistance); return; }
        AudioSource.PlayClipAtPoint(clip, at, volume);
    }

    static IEnumerator Co_Later(float seconds, System.Action action)
    {
        yield return new WaitForSeconds(seconds);
        action();
    }
}
