using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The player's melee weapon, a steel pipe. Key 1 (d-pad up) takes it out, 2 the gun (the mouse wheel flips between them,
/// see UpdateSelection); with no gun at all it is out on its own. All the motion is the user's own filmed takes (QuickMagic, AnimationTools/melee/qm_source.py):
/// out, he carries it upright in both hands (upper-body MeleeCarry layer over his own idle / walk / run); hold Aim to
/// raise it over his right shoulder (the filmed raise when standing), WASD walks and strafes (no sprint) and he keeps
/// facing the nearest enemy in front of him (with none, the mouse turns him).
/// Shoot = one committed two-handed attack (full body): a diagonal chop from the right shoulder down across in front of
/// him, a bent-over follow-through and the way back up into the ready pose (~1 s). He
/// can't move from the wind-up until late in the recovery and only turns during the wind-up, so a miss costs time; a
/// second press only counts late in the recovery (no spamming). A hit does <see cref="damage"/> (<see cref="downedDamage"/>
/// to an enemy down from a weak point hit), shoves the enemy back, hitstops, shakes the camera and plays a heavy impact;
/// hitting a wall clangs.
/// The hands are retargeted by position and closed round the stick in the bake (grip_bake.py hand_retarget), so the pipe
/// simply sits rigid in his right fist; Reattach() keeps his head and arms (which hang off c_traj) on his body wherever
/// clips are mixed. Built by Tools > Weapons > Build Melee Pipe (clips: AnimationTools/melee/melee_anims_build.py -- qm).
/// </summary>
public class PlayerMeleeWeapon : MonoBehaviour
{
    public static PlayerMeleeWeapon instance;
    /// <summary>The pipe is in his hand (instead of a gun).</summary>
    public static bool Equipped => instance != null && instance.equipped;
    /// <summary>Raised or swinging: this component moves and turns him, PlayerMovement stays out of it.</summary>
    public static bool Ready => instance != null && instance.ready;
    public static bool Attacking => instance != null && instance.attacking;
    /// <summary>Putting one weapon away and taking the other out (no aiming / raising meanwhile).</summary>
    public static bool Switching { get; private set; }
    /// <summary>A switch starts: true = to the pipe, false = to the gun.</summary>
    public static event System.Action<bool> OnSwitchStarted;
    /// <summary>Enemy hits / wall clangs so far (test benches read these).</summary>
    public int EnemyHits { get; private set; }
    public int WallHits { get; private set; }
    public int ProjectileHits { get; private set; }
    public Enemy Target => target;
    public bool Swinging => attacking;

    [Header("Setup")]
    [SerializeField] InputActionReference selectInput;
    [SerializeField] InputActionReference aimInput;
    [SerializeField] InputActionReference shootInput;
    [SerializeField] InputActionReference moveInput;
    [SerializeField] InputActionReference lookInput;
    [Tooltip("Takes the gun out instead (key 2) - when there is no PlayerWeaponSwitcher (the Gym's has its own gun keys).")]
    [SerializeField] InputActionReference gunSelectInput;
    [Tooltip("Mouse wheel: flips between the pipe and the gun.")]
    [SerializeField] InputActionReference switchInput;
    [Tooltip("Seconds to put one weapon away and take the other out.")]
    [SerializeField] float switchTime = 1f;
    [SerializeField] AudioClip gunDrawClip;
    [SerializeField] Item weaponItem;
    [Tooltip("The pipe in his right hand.")]
    [SerializeField] GameObject pipe;

    [Header("Animation")]
    [SerializeField] Animator animator;
    [Tooltip("Full-body layer: the stance + strafe walks (blend tree on MeleeX / MeleeY = local velocity in m/s) and the attack.")]
    [SerializeField] string moveLayerName = "Melee";
    [SerializeField] string moveState = "MeleeMove";
    [SerializeField] string attackState = "MeleeAttack";
    [Tooltip("Upper-body layer that carries the pipe upright in both hands while it's out and not raised.")]
    [SerializeField] string carryLayerName = "MeleeCarry";
    [Tooltip("Upper-body layer the filmed raise plays on - the same whether he stands, walks or runs (legs go on underneath).")]
    [SerializeField] string upperLayerName = "MeleeUpper";
    [SerializeField] string raiseState = "MeleeRaise";
    [Tooltip("Normalized time in the raise where it starts handing over to the ready stance, and how long that takes (s).")]
    [SerializeField] float raiseHandOver = 0.8f;
    [SerializeField] AnimationClip raiseClip;
    [SerializeField] float raiseBlendOut = 0.3f;
    [Tooltip("Playback speed of the raise (2 = real time, see attackSpeed; 1 = the slower wind-up the user prefers).")]
    [SerializeField] float raiseSpeed = 1f;
    [SerializeField] float carryBlend = 0.25f;
    [Tooltip("Lowest point on the pipe below its grip (the right fist) a hand can hold - the end of the tape.")]
    [SerializeField] float lowestGrip = 0.12f;
    [SerializeField] AnimationClip attackClip;
    [Tooltip("Seconds to blend into / out of the stance (eased in and out).")]
    [SerializeField] float raiseTime = 0.3f;
    [SerializeField] float lowerTime = 0.7f;

    [Header("Attack timing (normalized time in the attack clip)")]
    [Tooltip("Playback speed of the attack clip. The QuickMagic takes are filmed at 60 fps but stamped 30, so 2 = real time (~1 s with the recovery).")]
    [SerializeField] float attackSpeed = 1.7f;
    [Tooltip("Crossfade into the attack. Short: the clip starts on the stance grip, and a long blend between two two-handed poses pulls the fists apart.")]
    [SerializeField] float attackBlendIn = 0.08f;
    [Tooltip("End of the wind-up (pipe drawn back over the shoulder): the strike starts, turning stops.")]
    [SerializeField] float strikeFrom = 0.1f;
    [SerializeField] float contactAt = 0.175f;
    [Tooltip("A Shoot press from here on is remembered and swings again as soon as the recovery ends; earlier presses are ignored.")]
    [SerializeField] float bufferFrom = 0.55f;
    [Tooltip("The attack hands back to the stance here (blended).")]
    [SerializeField] float attackEnd = 0.93f;
    [SerializeField] float returnBlend = 0.4f;

    [Header("Movement while raised")]
    [SerializeField] float forwardSpeed = 1.25f;
    [SerializeField] float strafeSpeed = 1.15f;
    [SerializeField] float backSpeed = 1.0f;
    [SerializeField] float acceleration = 5f;
    [Tooltip("Smoothing of the strafe blend (seconds) - softens direction changes and starts / stops.")]
    [SerializeField] float blendDamp = 0.15f;
    [SerializeField] float turnSpeed = 360f;
    [Tooltip("Turning eases in and out over about this long (seconds); the turn speeds below are its top speed.")]
    [SerializeField] float turnSmoothTime = 0.12f;
    [Tooltip("Turn rate during the wind-up (he can still line the swing up); none after it.")]
    [SerializeField] float windupTurnSpeed = 240f;
    [Tooltip("Mouse turn rate when no enemy is targeted (degrees per unit of look input).")]
    [SerializeField] float mouseTurn = 0.12f;

    [Header("Targeting")]
    [SerializeField] float lockRange = 9f;
    [Tooltip("A new target must be within this angle of where he faces.")]
    [SerializeField] float lockAngle = 70f;
    [SerializeField] float keepRange = 12f;

    [Header("Hit")]
    [SerializeField] int damage = 50;
    [SerializeField] int downedDamage = 50;
    [Tooltip("Reach from his centre to the enemy's centre.")]
    [SerializeField] float reach = 2.0f;
    [SerializeField] float hitArc = 150f;
    [SerializeField] float knockbackScale = 2f;
    [Tooltip("Freeze on impact (seconds) - the weight of the hit.")]
    [SerializeField] float hitstop = 0.09f;
    [Tooltip("Third-person camera shake on a hit: degrees of jolt.")]
    [SerializeField] float shakeIntensity = 1.2f;
    [SerializeField] float shakeDuration = 0.18f;
    [SerializeField] GameObject sparkEffect;
    [Tooltip("Flying projectiles (the Bag Bearer's bags) the swing meets are knocked out of the air, as a bullet does: " +
             "checked every frame from the strike until this long (normalized clip time) after contact.")]
    [SerializeField] float projectileWindow = 0.06f;
    [Tooltip("Reach from his centre to a projectile, and how high it can be (m above his feet).")]
    [SerializeField] float projectileReach = 2.3f;
    [SerializeField] float projectileMaxHeight = 2.8f;
    [Tooltip("Shootable padlocks within this reach break like a bullet breaks them (the Store's storage-room door: the pipe " +
             "is the only weapon there).")]
    [SerializeField] float lockReach = 1.8f;

    [Header("Audio")]
    [SerializeField] AudioClip[] swingClips;
    [SerializeField] AudioClip[] fleshHitClips;
    [SerializeField] AudioClip[] wallHitClips;
    [SerializeField] AudioClip readyClip;
    [SerializeField] float swingVolume = 0.7f;
    [SerializeField] float hitVolume = 1f;
    [SerializeField] float wallVolume = 0.8f;

    Player player;
    Inventory inventory;
    PlayerWeaponSystem weapons;
    PlayerWeaponSwitcher switcher;
    PlayerWeaponEnabler enabler;
    CharacterController controller;

    bool equipped, selected, ready, hiddenForUse, hiddenForGrab;
    bool switching, putAway;
    float wheelReadyAt;
    int moveLayer = -1, carryLayer = -1, upperLayer = -1;
    float carryWeight, upperWeight, raiseElapsed;
    bool raising;
    float layerWeight, layerProgress;      // weight = eased progress
    float yawVelocity;
    Vector3 velocity;
    Enemy target;

    bool attacking;
    float attackT;          // normalized time in the attack clip
    bool contactDone, strikeStarted, queued, returned;
    readonly HashSet<Enemy> hitThisSwing = new HashSet<Enemy>();

    Transform hand, elbow, leftHand;
    // Khalid's rig (Auto-Rig Pro export) hangs the head and both arms straight off c_traj, not off the spine: wherever
    // animations are mixed (the carry layer's mask over his walk, the strafe blend tree, crossfades) they drift off the
    // neck / shoulders / elbows. Reattach() puts them back every frame (see there).
    struct Attach { public Transform anchor, joint; public Vector3 local; public Transform[] move; }
    Attach[] attachments;
    [Tooltip("c_root_master.x's rest pose (local to c_traj) in the melee clips - set by the builder.")]
    [SerializeField, HideInInspector] Vector3 carryRootPos;
    [SerializeField, HideInInspector] Quaternion carryRootRot = Quaternion.identity;
    [SerializeField, HideInInspector] bool hasCarryRoot;
    Vector3 pipeLocalPos;   // the grip in the right fist (the bake's fist centre), set by the builder
    Quaternion pipeLocalRot;
    float pipeTail;         // along the pipe (its up axis) from its origin: the taped end (the head is up the axis)
    [Tooltip("Cutscene clips (CutsceneHold.Timeline): the pipe carried in one hand near its taped end, the head hanging "
             + "back and this many degrees down from square to the forearm.")]
    [SerializeField] float oneHandCarryTilt = 15f;
    [Tooltip("Where the fist holds it then: metres in from the taped end.")]
    [SerializeField] float oneHandGrip = 0.12f;

    Transform shakeCam;
    Quaternion shakeCamRest;
    float shakeLeft, shakeTime, shakeAmp;

    static readonly int MeleeX = Animator.StringToHash("MeleeX");
    static readonly int MeleeY = Animator.StringToHash("MeleeY");
    static readonly int AttackSpeedParam = Animator.StringToHash("MeleeSwingSpeed");
    static readonly int RaiseSpeedParam = Animator.StringToHash("MeleeRaiseSpeed");
    int enemyMask, worldMask;
    readonly Collider[] overlap = new Collider[32];
    int projectileMask;

    void Awake()
    {
        instance = this;
        player = GetComponent<Player>();
        inventory = GetComponent<Inventory>();
        weapons = GetComponent<PlayerWeaponSystem>();
        switcher = GetComponent<PlayerWeaponSwitcher>();
        enabler = GetComponent<PlayerWeaponEnabler>();
        controller = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        enemyMask = LayerMask.GetMask("Enemy");
        worldMask = ~LayerMask.GetMask("Enemy", "Player", "Ignore Raycast", "UI", "Projectile");
        projectileMask = LayerMask.GetMask("Projectile");
        if (pipe != null)
        {
            pipe.SetActive(false);
            hand = pipe.transform.parent;
            pipeLocalPos = pipe.transform.localPosition;
            pipeLocalRot = pipe.transform.localRotation;
            // the elbow: first bone up the chain that is a forearm's length away (skips twist / helper bones)
            for (var t = hand != null ? hand.parent : null; t != null; t = t.parent)
                if ((t.position - hand.position).magnitude > 0.15f) { elbow = t; break; }
            if (animator != null)
                foreach (var t in animator.GetComponentsInChildren<Transform>(true))
                    if (t.name == "hand.l") { leftHand = t; break; }
            // the pipe's ends along its length (its origin is the grip near the taped end)
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var mf in pipe.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    float y = pipe.transform.InverseTransformPoint(mf.transform.TransformPoint(corner)).y;
                    lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
                }
            }
            pipeTail = lo < hi ? lo : 0f;
            BuildAttachments();
        }
    }

    void Start()
    {
        CaptureAttachments();
    }

    // The joint offsets the re-attach keeps (neck -> head, upper arm -> forearm) are measured on one frame of the ready
    // stance played alone (a single clip is always attached) - the prefab's saved pose doesn't match the clips.
    void CaptureAttachments()
    {
        if (animator == null || attachments == null) return;
        moveLayer = animator.GetLayerIndex(moveLayerName);
        carryLayer = animator.GetLayerIndex(carryLayerName);
        upperLayer = animator.GetLayerIndex(upperLayerName);
        if (moveLayer < 0) return;
        float w = animator.GetLayerWeight(moveLayer);
        animator.SetLayerWeight(moveLayer, 1f);
        animator.SetFloat(MeleeX, 0f);
        animator.SetFloat(MeleeY, 0f);
        animator.Play(moveState, moveLayer, 0f);
        animator.Update(0f);
        for (int i = 0; i < attachments.Length; i++)
            attachments[i].local = attachments[i].anchor.InverseTransformPoint(attachments[i].joint.position);
        animator.SetLayerWeight(moveLayer, w);
        animator.Update(0f);
    }

    void OnEnable()
    {
        foreach (var a in new[] { selectInput, aimInput, shootInput, moveInput, lookInput, gunSelectInput, switchInput })
            if (a != null && a.action != null) a.action.Enable();
    }

    void OnDisable()
    {
        if (instance == this) { ready = false; attacking = false; switching = putAway = false; Switching = false; }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void BuildAttachments()
    {
        if (animator == null) return;
        var bones = new Dictionary<string, Transform>();
        foreach (var t in animator.GetComponentsInChildren<Transform>(true)) if (!bones.ContainsKey(t.name)) bones[t.name] = t;
        Transform B(string n) => bones.TryGetValue(n, out var t) ? t : null;
        rootMaster = B("c_root_master.x");
        spineBase = B("c_spine_01.x");
        cTraj = B("c_traj");
        var list = new List<Attach>();
        void Add(string anchor, string joint, params string[] move)
        {
            Transform a = B(anchor), j = B(joint);
            var m = new List<Transform>();
            foreach (var n in move) if (B(n) != null && B(n).parent == cTraj) m.Add(B(n));
            if (a == null || j == null || m.Count == 0) return;
            list.Add(new Attach { anchor = a, joint = j, local = a.InverseTransformPoint(j.position), move = TopMost(m) });
        }
        // the reliable joints (the shoulder bone itself isn't: its frame turns against the arm root even inside one clip)
        Add("neck.x", "head.x", "head_scale_fix.x");
        foreach (var s in new[] { "l", "r" }) Add("arm_stretch." + s, "forearm." + s, "forearm_stretch." + s, "forearm." + s);
        attachments = list.ToArray();
        // everything that hangs straight off c_traj above the waist: the head and both arms. ONLY direct children of c_traj:
        // their positions are animated, so the animator rewrites them every frame. (arm_twist hangs off the shoulder and no
        // clip animates its position - Unity never rewrites it, so moving it here would add up frame after frame.)
        var flat = new List<Transform>();
        foreach (var n in new[] { "head_scale_fix.x", "arm_stretch.l", "forearm_stretch.l", "forearm.l", "arm_stretch.r", "forearm_stretch.r", "forearm.r" })
            if (B(n) != null && B(n).parent == cTraj) flat.Add(B(n));
        upperFlat = TopMost(flat);
    }

    static Transform[] TopMost(List<Transform> m)
    {
        m.RemoveAll(t => { for (var p = t.parent; p != null; p = p.parent) if (m.Contains(p)) return true; return false; });
        return m.ToArray();
    }

    Transform rootMaster, cTraj, spineBase;
    Transform[] upperFlat;

    /// <summary>Test switch: 0 = off, 2 = on.</summary>
    public static int reattachMode = 2;

    void Reattach()
    {
        if (attachments == null) return;
        // The carry layer takes his spine (under c_root_master), arms and head (straight under c_traj) from the filmed carry,
        // and his root master from his own walk / run. The melee clips are baked with the root master at rest, so the arms
        // and head belong to a rest root master: move them by exactly how far his root master is from it.
        float upperOn = Mathf.Max(carryLayer >= 0 ? animator.GetLayerWeight(carryLayer) : 0f, upperLayer >= 0 ? animator.GetLayerWeight(upperLayer) : 0f);
        float w = upperOn * (1f - layerWeight) * (1f - PlayerCloseCombat.LayerWeight);
        if (hasCarryRoot && w > 0.001f && rootMaster != null && cTraj != null && spineBase != null)
        {
            // his walk / run moves and turns the root master. Keep its forward lean (pitch about his right axis - a run
            // leans well forward) and take out its sway (yaw / roll - that rocked his head), by turning the spine on its
            // base (rotation only: no clip animates its position, so Unity wouldn't rewrite a moved position); then put
            // the arms and head where that leaves the filmed upper body: turned by the kept lean, riding the spine base
            Quaternion rest = cTraj.rotation * carryRootRot;
            Quaternion dR = rootMaster.rotation * Quaternion.Inverse(rest);
            Vector3 r = transform.right;
            Vector3 v = Vector3.Project(new Vector3(dR.x, dR.y, dR.z), r);
            Quaternion lean = new Quaternion(v.x, v.y, v.z, dR.w);
            float m = Mathf.Sqrt(lean.x * lean.x + lean.y * lean.y + lean.z * lean.z + lean.w * lean.w);
            lean = m > 1e-5f ? new Quaternion(lean.x / m, lean.y / m, lean.z / m, lean.w / m) : Quaternion.identity;
            Matrix4x4 restM = cTraj.localToWorldMatrix * Matrix4x4.TRS(carryRootPos, carryRootRot, rootMaster.localScale);
            Vector3 baseRest = restM.MultiplyPoint3x4(spineBase.localPosition);
            spineBase.rotation = Quaternion.Slerp(Quaternion.identity, lean * Quaternion.Inverse(dR), w) * spineBase.rotation;
            Vector3 baseNow = spineBase.position;
            foreach (var t in upperFlat)
            {
                Vector3 pos = baseNow + lean * (t.position - baseRest);
                t.SetPositionAndRotation(Vector3.Lerp(t.position, pos, w), Quaternion.Slerp(t.rotation, lean * t.rotation, w));
            }
        }
        // then joint by joint what mixing clips leaves (blend trees, crossfades): head on the neck, forearms on the elbows
        foreach (var a in attachments)
        {
            Vector3 d = a.anchor.TransformPoint(a.local) - a.joint.position;
            if (d.sqrMagnitude < 2.5e-7f) continue;                // under half a millimetre
            foreach (var t in a.move) t.position += d;
        }
    }

    /// <summary>Takes the pipe out (as key 3 does) once it is in the inventory.</summary>
    public void Select() => selected = true;

    /// <summary>Hides the pipe while something else is in his hand (a healing item).</summary>
    public static void SetHiddenForUse(bool hidden)
    {
        if (instance == null) return;
        instance.hiddenForUse = hidden;
        instance.RefreshPipe();
    }

    /// <summary>How a cutscene holds the pipe. None = normal play. Away: put away (a scene with the gun). Carry / Raised: his
    /// Animator Controller poses him as in play - carried, or the raised stance (the coward meeting without a gun). Timeline:
    /// a cutscene clip poses him, so the pipe just hangs along his forearm and the carry layer fixes stay off.</summary>
    public enum CutsceneHold { None, Away, Carry, Raised, Timeline }
    CutsceneHold cutscene;

    public static void SetCutscene(CutsceneHold hold)
    {
        if (instance != null) instance.cutscene = hold;
    }

    /// <summary>Hides the pipe while a Questioner has hold of him (it pokes into the grab camera's shot).</summary>
    public static void SetHiddenForGrab(bool hidden)
    {
        if (instance == null) return;
        instance.hiddenForGrab = hidden;
        instance.RefreshPipe();
    }

    void Update()
    {
        if (moveLayer < 0 && animator != null)
        {
            moveLayer = animator.GetLayerIndex(moveLayerName);
            carryLayer = animator.GetLayerIndex(carryLayerName);
            upperLayer = animator.GetLayerIndex(upperLayerName);
        }

        UpdateShake();
        if (player.IsDead()) { if (equipped) SetEquipped(false); ApplyWeights(0f, true); return; }
        if (GameManager.IsPaused) return;
        if (cutscene != CutsceneHold.None) { UpdateCutscene(); return; }

        UpdateSelection();
        if (!equipped) { ApplyWeights(0f, false); return; }

        bool canRaise = CanRaise();
        bool aimHeld = aimInput != null && aimInput.action.ReadValue<float>() > 0.1f;

        if (!ready && aimHeld && canRaise) Raise();
        else if (ready && !canRaise) Lower();
        else if (ready && !attacking && !aimHeld) Lower();

        if (ready)
        {
            if (shootInput != null && shootInput.action.WasPressedThisFrame())
            {
                if (!attacking) StartAttack();
                else if (attackT >= bufferFrom) queued = true;
            }
            if (attacking) UpdateAttack(aimHeld);
            UpdateFacing();
            UpdateMovement();
        }

        ApplyWeights(ready ? 1f : 0f, false);
    }

    void LateUpdate()
    {
        if (equipped && reattachMode > 0) Reattach();
        if (equipped && pipe != null && pipe.activeSelf) AimPipe();

        // the gun object is switched back on by inventory updates (PlayerWeaponEnabler) - the pipe is out, keep it away
        if (equipped && enabler != null && enabler.WeaponObject != null && enabler.WeaponObject.activeSelf)
            enabler.WeaponObject.SetActive(false);
    }

    // ---------------- equip ----------------

    // 1 = the pipe, 2 = the gun (the Gym's switcher: its own gun keys), the mouse wheel flips between them. Only one
    // weapon owned: that one. Picking the gun up while the pipe is out leaves the pipe out (the tutorial then shows how
    // to switch). Between the two it takes switchTime (Co_Switch); the first weapon just comes out.
    void UpdateSelection()
    {
        bool owns = weaponItem != null && inventory != null && inventory.HasItem(weaponItem);
        bool hasGun = inventory != null && inventory.HasWeapon();
        if (!owns) selected = false;
        else if (!hasGun) selected = true;
        else if (!switching && !ready && !PlayerQuickHeal.Using && (weapons == null || (!weapons.isReloading && !weapons.isAiming)))
        {
            if (selectInput != null && selectInput.action.WasPressedThisFrame()) selected = true;
            if (GunKeyPressed()) selected = false;
            float wheel = switchInput != null ? switchInput.action.ReadValue<float>() : 0f;
            if (Mathf.Abs(wheel) > 0.01f && Time.unscaledTime >= wheelReadyAt) { selected = !selected; wheelReadyAt = Time.unscaledTime + 0.25f; }
        }

        if (selected == equipped || switching) return;
        if (owns && hasGun && !player.IsDead()) StartCoroutine(Co_Switch(selected));
        else SetEquipped(selected);
    }

    bool GunKeyPressed()
    {
        if (switcher != null)
        {
            foreach (var g in switcher.guns)
                if (g.selectInput != null && g.selectInput.action.WasPressedThisFrame() && inventory.HasItem(g.weaponItem)) return true;
            return false;
        }
        return gunSelectInput != null && gunSelectInput.action.WasPressedThisFrame();
    }

    // Putting one away and taking the other out: the hands drop the weapon that is out to his side, it is swapped there
    // (with the draw sound) and the new one comes up - the pipe into the two-handed carry, the gun hangs in his hand.
    // No aiming / raising until it is done.
    IEnumerator Co_Switch(bool toPipe)
    {
        switching = true;
        Switching = true;
        OnSwitchStarted?.Invoke(toPipe);
        Lower();
        if (weapons != null && weapons.isAiming) weapons.ExitOutOfAiming();
        float swapAt = switchTime * (toPipe ? 0.35f : 0.45f);
        if (!toPipe) putAway = true;                 // the carry lets go: his arms bring the pipe down to his side
        yield return new WaitForSeconds(swapAt);
        putAway = false;
        SetEquipped(toPipe);
        if (!toPipe) Play2D(gunDrawClip, 0.8f);
        yield return new WaitForSeconds(switchTime - swapAt);
        switching = false;
        Switching = false;
    }

    // no input while a cutscene holds the pipe: it only says how (see CutsceneHold)
    void UpdateCutscene()
    {
        bool want = cutscene != CutsceneHold.Away && weaponItem != null && inventory != null && inventory.HasItem(weaponItem);
        if (want != equipped) SetEquipped(want);
        if (!equipped) { ApplyWeights(0f, false); return; }
        if (cutscene == CutsceneHold.Raised)
        {
            if (!ready)
            {
                Raise();
                // the stance standing still, whatever speed he walked in at
                velocity = Vector3.zero;
                if (animator != null) { animator.SetFloat(MeleeX, 0f); animator.SetFloat(MeleeY, 0f); }
            }
        }
        else Lower();
        ApplyWeights(ready ? 1f : 0f, false);
    }

    void SetEquipped(bool on)
    {
        if (on == equipped) return;
        if (!on) Lower();
        equipped = on;
        if (on)
        {
            if (weapons != null && weapons.isAiming) weapons.ExitOutOfAiming();
            if (enabler != null && enabler.WeaponObject != null) enabler.WeaponObject.SetActive(false);
            Play2D(readyClip, 0.8f);
        }
        else if (enabler != null && enabler.WeaponObject != null && weapons != null && weapons.weaponIsEnabled && !player.IsDead())
        {
            enabler.WeaponObject.SetActive(true);
        }
        RefreshPipe();
    }

    void RefreshPipe()
    {
        if (pipe != null) pipe.SetActive(equipped && !hiddenForUse && !hiddenForGrab);
    }

    bool CanRaise()
    {
        if (switching) return false;
        if (player.pauseMovement || player.isScared || player.isRolling || player.isGrabbed || PlayerQuickHeal.Using) return false;
        if (EyePeakHandler.instance != null && EyePeakHandler.instance.IsEyePeakActivated()) return false;
        if (LevelManager.instance != null && (LevelManager.instance.isGameOver || LevelManager.instance.isGameWon)) return false;
        return true;
    }

    // ---------------- raise / lower ----------------

    void Raise()
    {
        ready = true;
        attacking = false;
        velocity = transform.forward * Mathf.Min(player.playerMovement.CurrentSpeed, forwardSpeed);
        target = null;
        yawVelocity = 0f;
        if (animator != null && moveLayer >= 0)
        {
            // start the strafe blend where he already is (walking in = walking stance), and if the layer is still fading out
            // from the last time (an attack's tail), blend over to the stance instead of popping
            Vector3 local = transform.InverseTransformDirection(velocity);
            animator.SetFloat(MeleeX, local.x);
            animator.SetFloat(MeleeY, local.z);
            animator.CrossFadeInFixedTime(moveState, layerWeight > 0.01f ? returnBlend : 0f, moveLayer, 0f);
            // the filmed raise on the upper body, standing or on the move (his legs go straight into the stance's)
            raising = upperLayer >= 0 && animator.HasState(upperLayer, Animator.StringToHash(raiseState));
            if (raising)
            {
                raiseElapsed = 0f;
                animator.SetFloat(RaiseSpeedParam, raiseSpeed);
                animator.Play(raiseState, upperLayer, 0f);
                upperWeight = 1f;
                animator.SetLayerWeight(upperLayer, 1f);
            }
        }
        Play2D(readyClip, 0.6f);
    }

    void Lower()
    {
        if (!ready) return;
        ready = false;
        attacking = false;
        raising = false;
        queued = false;
        target = null;
        velocity = Vector3.zero;
    }

    void ApplyWeights(float targetWeight, bool instant)
    {
        if (animator == null || moveLayer < 0) return;
        float time = targetWeight > layerProgress ? raiseTime : lowerTime;
        layerProgress = instant ? targetWeight : Mathf.MoveTowards(layerProgress, targetWeight, Time.deltaTime / Mathf.Max(0.01f, time));
        layerWeight = layerProgress * layerProgress * (3f - 2f * layerProgress);
        animator.SetLayerWeight(moveLayer, layerWeight);

        // the raise layer: on while raising, handed over to the stance near its end, cut fast by an attack or lowering
        if (upperLayer >= 0)
        {
            if (raising)
            {
                // own clock: right after Play() the layer still reports the previous (finished) raise for a frame
                raiseElapsed += Time.deltaTime;
                float len = raiseClip != null ? raiseClip.length : 1.07f;
                if (!ready || attacking || raiseElapsed >= raiseHandOver * len / Mathf.Max(0.01f, raiseSpeed)) raising = false;
            }
            float fade = attacking ? 0.08f : raiseBlendOut;
            upperWeight = instant ? (raising ? 1f : 0f) : Mathf.MoveTowards(upperWeight, raising ? 1f : 0f, Time.deltaTime / fade);
            animator.SetLayerWeight(upperLayer, upperWeight * upperWeight * (3f - 2f * upperWeight));
        }

        // the upright two-handed carry, whenever the pipe is in his hands
        if (carryLayer >= 0)
        {
            float want = equipped && !hiddenForUse && !putAway && !player.IsDead() && cutscene != CutsceneHold.Timeline ? 1f : 0f;
            float blend = switching ? switchTime * 0.45f : carryBlend;
            carryWeight = instant ? want : Mathf.MoveTowards(carryWeight, want, Time.deltaTime / Mathf.Max(0.01f, blend));
            animator.SetLayerWeight(carryLayer, carryWeight * carryWeight * (3f - 2f * carryWeight));
        }
    }

    float ClipLength => attackClip != null ? attackClip.length : 2f;

    // ---------------- attack ----------------

    void StartAttack()
    {
        attacking = true;
        raising = false;
        attackT = 0f;
        contactDone = strikeStarted = queued = returned = false;
        hitThisSwing.Clear();
        // committed: he stops where he stands (the filmed swing carries its own step) and can't move until it's over
        velocity = Vector3.zero;
        animator.SetFloat(MeleeX, 0f);
        animator.SetFloat(MeleeY, 0f);
        animator.SetFloat(AttackSpeedParam, attackSpeed);
        animator.CrossFadeInFixedTime(attackState, attackBlendIn, moveLayer, 0f);
    }

    void UpdateAttack(bool aimHeld)
    {
        attackT += Time.deltaTime * attackSpeed / ClipLength;

        if (!strikeStarted && attackT >= strikeFrom)
        {
            strikeStarted = true;
            Play2D(Pick(swingClips), swingVolume);
        }

        // bags in flight: the whole strike, not just the contact frame (they move a metre or more in that time)
        if (strikeStarted && attackT <= contactAt + projectileWindow) BatProjectiles();

        if (!contactDone && attackT >= contactAt)
        {
            contactDone = true;
            DoHit();
        }

        if (!returned && attackT >= attackEnd)
        {
            returned = true;
            animator.CrossFadeInFixedTime(moveState, returnBlend, moveLayer, 0f);
        }

        if (attackT >= attackEnd)
        {
            attacking = false;
            if (queued && aimHeld) StartAttack();
        }
    }

    void DoHit()
    {
        Vector3 origin = transform.position + Vector3.up * 1.2f;
        int n = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 1f + transform.forward * reach * 0.5f, reach + 0.5f, overlap, enemyMask, QueryTriggerInteraction.Collide);
        bool hitAny = false;
        for (int i = 0; i < n; i++)
        {
            var enemy = overlap[i].GetComponentInParent<Enemy>();
            if (enemy == null || hitThisSwing.Contains(enemy) || enemy.health == null || enemy.health.IsDead) continue;
            Vector3 to = enemy.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > reach + enemy.MeleeReachBonus) continue;
            if (to.magnitude > 0.6f && Vector3.Angle(transform.forward, to) > hitArc * 0.5f) continue;
            Vector3 chest = enemy.transform.position + Vector3.up * 1.2f;
            if (Physics.Linecast(origin, chest, worldMask, QueryTriggerInteraction.Ignore)) continue;

            hitThisSwing.Add(enemy);
            hitAny = true;
            EnemyHits++;
            Vector3 point = overlap[i].ClosestPoint(origin);
            if ((point - origin).sqrMagnitude < 0.0001f) point = chest - to.normalized * 0.3f;
            DamageEnemy(enemy, point);
        }

        if (hitAny)
        {
            Play2D(Pick(fleshHitClips), hitVolume);
            if (HitstopManager.instance != null) HitstopManager.instance.FreezeTime(hitstop);
            Shake(shakeIntensity, shakeDuration);
            return;
        }

        // nothing alive in reach: a padlock in front of him?
        if (BreakLocks(origin))
        {
            // no metal clang - the lock plays its own break sound
            if (HitstopManager.instance != null) HitstopManager.instance.FreezeTime(hitstop * 0.6f);
            Shake(shakeIntensity * 0.7f, shakeDuration);
            return;
        }

        // does the pipe meet a wall along its arc?
        const float sweep = -1f;                  // the swing comes from his right
        for (int k = 0; k < 5; k++)
        {
            Vector3 dir = Quaternion.AngleAxis(sweep * (60f - k * 30f), Vector3.up) * transform.forward;
            if (Physics.Raycast(origin, dir, out var wall, 1.35f, worldMask, QueryTriggerInteraction.Ignore))
            {
                WallHits++;
                if (SoundEffectManager.instance != null) SoundEffectManager.instance.PlaySFXAtPosition(Pick(wallHitClips), wall.point, wallVolume);
                if (sparkEffect != null) Destroy(Instantiate(sparkEffect, wall.point, Quaternion.LookRotation(wall.normal)), 3f);
                Shake(shakeIntensity * 0.5f, shakeDuration);
                break;
            }
        }
    }

    /// <summary>Breaks shootable padlocks in the swing's arc, the same as a bullet (Lock.LockDamaged). Puzzle locks stay.</summary>
    bool BreakLocks(Vector3 origin)
    {
        bool broke = false;
        int n = Physics.OverlapSphereNonAlloc(origin + transform.forward * lockReach * 0.5f, lockReach, overlap, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var padlock = overlap[i].GetComponentInParent<Lock>();
            if (padlock == null || !padlock.IsShootable || !padlock.isActiveAndEnabled) continue;
            Vector3 to = padlock.transform.position - transform.position;
            float height = to.y;
            to.y = 0f;
            if (to.magnitude > lockReach || height < 0.2f || height > 2.4f) continue;
            if (to.magnitude > 0.5f && Vector3.Angle(transform.forward, to) > hitArc * 0.5f) continue;

            if (sparkEffect != null) Destroy(Instantiate(sparkEffect, padlock.transform.position, Quaternion.LookRotation(-to.normalized)), 3f);
            padlock.LockDamaged();
            broke = true;
        }
        return broke;
    }

    /// <summary>Knocks flying projectiles in the swing's arc out of the air - the same as shooting one (it bursts and drops
    /// its "shot by bullet" loot).</summary>
    void BatProjectiles()
    {
        Vector3 centre = transform.position + Vector3.up * 1.2f + transform.forward * projectileReach * 0.4f;
        int n = Physics.OverlapSphereNonAlloc(centre, projectileReach, overlap, projectileMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var projectile = overlap[i].GetComponentInParent<Projectile>();
            if (projectile == null || !projectile.gameObject.activeInHierarchy) continue;
            // a bag wedged in a door is a padlock first (Store storage room), like it is for bullets: BreakLocks opens the door
            var asLock = projectile.GetComponentInParent<Lock>();
            if (asLock != null && asLock.IsShootable) continue;
            Vector3 to = projectile.transform.position - transform.position;
            float height = to.y;
            to.y = 0f;
            if (to.magnitude > projectileReach || height < -0.3f || height > projectileMaxHeight) continue;
            if (to.magnitude > 0.6f && Vector3.Angle(transform.forward, to) > hitArc * 0.5f) continue;

            ProjectileHits++;
            projectile.DestoryProjectile(damage);         // its own burst sound, as when it's shot
            if (HitstopManager.instance != null) HitstopManager.instance.FreezeTime(hitstop * 0.6f);
            Shake(shakeIntensity * 0.6f, shakeDuration);
        }
    }

    void DamageEnemy(Enemy enemy, Vector3 point)
    {
        bool downed = enemy.IsDownedForMelee;
        enemy.health.isDamageByWeakpointHit = false;
        enemy.health.SetLastHitPoint(point);
        enemy.health.SetDamageDirection(enemy.transform.position - transform.position);
        enemy.pendingKnockbackScale = knockbackScale;
        enemy.pendingMeleeHit = true;
        enemy.health.Damage(downed ? downedDamage : damage);
        enemy.pendingMeleeHit = false;
        enemy.pendingKnockbackScale = 1f;
    }

    // ---------------- facing / movement ----------------

    void UpdateFacing()
    {
        target = PickTarget();
        // committed: he lines the swing up during the wind-up, then it goes where it goes until he has recovered (the turn
        // he was making still eases out rather than stopping dead)
        if (attacking && attackT >= strikeFrom) { TurnTo(transform.eulerAngles.y, turnSpeed); return; }
        float rate = attacking ? windupTurnSpeed : turnSpeed;
        if (target != null)
        {
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) TurnTo(Quaternion.LookRotation(to).eulerAngles.y, rate);
        }
        else if (CameraSystem.Instance != null && CameraSystem.Instance.FreeCameraEnabled && Camera.main != null)
        {
            Vector3 f = Camera.main.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 0.01f) TurnTo(Quaternion.LookRotation(f).eulerAngles.y, rate);
        }
        else if (lookInput != null)
        {
            float sens = weapons != null ? weapons.Sensitivity : 1f;
            float yaw = lookInput.action.ReadValue<Vector2>().x * mouseTurn * sens;
            transform.Rotate(0f, Mathf.Clamp(yaw, -rate * Time.deltaTime, rate * Time.deltaTime), 0f);
        }
    }

    void TurnTo(float yaw, float maxRate)
    {
        float y = Mathf.SmoothDampAngle(transform.eulerAngles.y, yaw, ref yawVelocity, turnSmoothTime, maxRate);
        transform.rotation = Quaternion.Euler(0f, y, 0f);
    }

    Enemy PickTarget()
    {
        if (target != null && Valid(target, keepRange, 360f)) return target;
        Enemy best = null;
        float bestScore = float.MaxValue;
        int n = Physics.OverlapSphereNonAlloc(transform.position, lockRange, overlap, enemyMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var e = overlap[i].GetComponentInParent<Enemy>();
            if (e == null || e == best || !Valid(e, lockRange, lockAngle)) continue;
            Vector3 to = e.transform.position - transform.position;
            to.y = 0f;
            float score = to.magnitude + Vector3.Angle(transform.forward, to) * 0.03f;
            if (score < bestScore) { bestScore = score; best = e; }
        }
        return best;
    }

    bool Valid(Enemy e, float range, float angle)
    {
        if (e == null || !e.isActiveAndEnabled || e.health == null || e.health.IsDead) return false;
        Vector3 to = e.transform.position - transform.position;
        if (Mathf.Abs(to.y) > 2.5f) return false;
        to.y = 0f;
        if (to.magnitude > range) return false;
        if (angle < 180f && Vector3.Angle(transform.forward, to) > angle) return false;
        return !Physics.Linecast(transform.position + Vector3.up * 1.4f, e.transform.position + Vector3.up * 1.4f, worldMask, QueryTriggerInteraction.Ignore);
    }

    void UpdateMovement()
    {
        Vector2 input = moveInput != null ? moveInput.action.ReadValue<Vector2>() : Vector2.zero;
        input = Vector2.ClampMagnitude(input, 1f);
        float speed = input.y < -0.1f ? backSpeed : (Mathf.Abs(input.x) > Mathf.Abs(input.y) ? strafeSpeed : forwardSpeed);
        // planted for the whole attack
        if (attacking) input = Vector2.zero;
        Vector3 wish = (transform.right * input.x + transform.forward * input.y) * speed * PlayerCloseCombat.SpeedMultiplier;
        velocity = Vector3.MoveTowards(velocity, wish, acceleration * Time.deltaTime);
        // a Questioner's hit shoves him back (and drives the strafe steps)
        Vector3 knock = PlayerCloseCombat.KnockVelocity;
        if (knock.sqrMagnitude > 0.0001f) velocity = knock;

        if (controller != null && controller.enabled) controller.Move(velocity * Time.deltaTime);

        Vector3 local = transform.InverseTransformDirection(velocity);
        animator.SetFloat(MeleeX, local.x, blendDamp, Time.deltaTime);
        animator.SetFloat(MeleeY, local.z, blendDamp, Time.deltaTime);
    }

    // ---------------- pipe direction ----------------

    void AimPipe()
    {
        // a cutscene's walk / idle clips: held in one hand near the taped end, across the fist, the head hanging back
        // and down - it swings with his arm
        if (cutscene == CutsceneHold.Timeline && hand != null && elbow != null)
        {
            Vector3 arm = (hand.position - elbow.position).normalized;
            Vector3 behind = Vector3.ProjectOnPlane(-transform.forward, arm);
            if (behind.sqrMagnitude < 1e-4f) behind = -transform.forward;
            behind.Normalize();
            float tilt = oneHandCarryTilt * Mathf.Deg2Rad;
            Vector3 along = (behind * Mathf.Cos(tilt) + arm * Mathf.Sin(tilt)).normalized;
            // square to the forearm tips it up / down with every arm swing - the wrist keeps it nearer a steady angle
            Vector3 steady = (-transform.forward * Mathf.Cos(tilt) - Vector3.up * Mathf.Sin(tilt)).normalized;
            along = Vector3.Slerp(along, steady, 0.6f);
            pipe.transform.rotation = Quaternion.LookRotation(along, -arm) * Quaternion.Euler(90f, 0f, 0f);
            pipe.transform.position = hand.TransformPoint(pipeLocalPos) - pipe.transform.TransformVector(Vector3.up * pipeTail) - along * oneHandGrip;
            return;
        }

        // raised: rigid in the right fist, exactly as baked (thumb along the pipe)
        pipe.transform.localPosition = pipeLocalPos;
        pipe.transform.localRotation = pipeLocalRot;
        // a wide grip (the filmed carry has the fists ~30 cm apart): the pipe slides down through the right fist until
        // its end is under the left fist, instead of the left hand holding air below it
        if (leftHand != null)
        {
            Vector3 up = pipe.transform.up;
            Vector3 leftFist = leftHand.position + leftHand.up * FistOffset.y + leftHand.forward * FistOffset.z;
            float along = Vector3.Dot(leftFist - pipe.transform.position, up);
            if (along < -lowestGrip) pipe.transform.position += up * (along + lowestGrip);
        }
        float held = Mathf.Max(layerWeight, carryLayer >= 0 ? animator.GetLayerWeight(carryLayer) : 0f);
        if (held >= 0.999f || hand == null || elbow == null) return;

        // lowered: hangs along the forearm (the locomotion clips don't close the hand round it); eased with the layer
        Vector3 forearm = (hand.position - elbow.position).normalized;
        Vector3 across = Vector3.Cross(forearm, transform.forward);
        if (across.sqrMagnitude < 1e-4f) across = transform.right;
        Quaternion carry = Quaternion.LookRotation(forearm, across) * Quaternion.Euler(90f, 0f, 0f);
        pipe.transform.rotation = Quaternion.Slerp(carry, pipe.transform.rotation, held);
    }

    static readonly Vector3 FistOffset = new Vector3(0f, 0.065f, -0.025f);   // wrist -> centre of the closed fist, hand space (m)

    // ---------------- camera shake ----------------

    // The third-person view comes from the CameraSystem rig's virtual camera; nothing else moves that camera inside the
    // rig, so a local rotation jolt on it is safe (it runs in Update, before the rig and the Cinemachine brain).
    void Shake(float degrees, float duration)
    {
        if (shakeCam == null && CameraSystem.Instance != null)
        {
            var vcam = CameraSystem.Instance.GetComponentInChildren<CinemachineVirtualCamera>(true);
            if (vcam != null) { shakeCam = vcam.transform; shakeCamRest = shakeCam.localRotation; }
        }
        if (shakeCam == null) return;
        shakeAmp = degrees;
        shakeTime = shakeLeft = duration;
    }

    void UpdateShake()
    {
        if (shakeCam == null || shakeLeft <= 0f) return;
        shakeLeft -= Time.unscaledDeltaTime;     // runs through the hitstop
        if (shakeLeft <= 0f) { shakeCam.localRotation = shakeCamRest; return; }
        float a = shakeAmp * (shakeLeft / shakeTime);
        shakeCam.localRotation = shakeCamRest * Quaternion.Euler(Random.Range(-a, a), Random.Range(-a, a), Random.Range(-a, a) * 0.5f);
    }

    // ---------------- audio ----------------

    static AudioClip Pick(AudioClip[] clips)
    {
        return clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;
    }

    static void Play2D(AudioClip clip, float volume)
    {
        if (clip != null && SoundEffectManager.instance != null) SoundEffectManager.instance.PlaySFX(clip, volume, true);
    }
}
