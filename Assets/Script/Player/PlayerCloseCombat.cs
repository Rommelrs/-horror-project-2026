using Cinemachine;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Khalid's side of the Questioner fight (Questioner spec, player melee basics):
///  - Shove (short range, cooldown 2 s): stops a Questioner's grab wind-up / rush; while held it's the escape button (mash).
///  - Grabbed: locked facing the Questioner, mash Shove inside the window to push him off, otherwise he bites.
///  - The Question (scream): slows him 30% and sways his aim for a moment.
///  - Dodge (Roll key): a quick dash (back by default, or the held direction) whose first ~0.3 s attacks that honour
///    i-frames pass through (the Questioner's grab and swipes).
/// Khalid's reaction states (Grabbed / BreakFree / Bitten / Shove) play on the "CloseCombat" animator layer when it has them.
/// </summary>
public class PlayerCloseCombat : MonoBehaviour
{
    public static PlayerCloseCombat instance;

    [SerializeField] InputActionReference shoveAction;
    [SerializeField] float shoveRange = 1.7f;
    [SerializeField] float shoveArc = 100f;
    [SerializeField] float shoveCooldown = 2f;
    [Tooltip("Seconds the shove locks his movement (the push itself).")]
    [SerializeField] float shoveTime = 0.45f;

    [Header("Dodge (Roll key)")]
    [SerializeField] InputActionReference dodgeAction;
    [SerializeField] float dodgeDistance = 1.7f;
    [SerializeField] float dodgeTime = 0.32f;
    [Tooltip("Invulnerable from the start of the dodge for this long (spec: about 0.3 s).")]
    [SerializeField] float dodgeIFrames = 0.3f;
    [SerializeField] float dodgeCooldown = 0.7f;
    [SerializeField] float dodgeStamina = 12f;

    [Header("Grab camera (side-on close shot of the two faces while he holds Khalid)")]
    [SerializeField] float grabCamDistance = 1.9f;
    [SerializeField] float grabCamPushIn = 0.3f;
    [SerializeField] float grabCamFov = 42f;
    [SerializeField] float grabCamBlend = 0.35f;

    [Header("Scream sway")]
    [Tooltip("Aim sway amplitude while shaken, in bullet-spread units (~0.08 = 4.5 deg).")]
    [SerializeField] float swayAmount = 0.08f;
    [SerializeField] float swayCameraDegrees = 2.5f;

    [Header("Animator (CloseCombat layer)")]
    [SerializeField] string layerName = "CloseCombat";
    [SerializeField] float layerBlend = 0.12f;

    [Header("Audio")]
    public AudioClip[] shoveSFX;
    public AudioClip[] struggleSFX;
    public AudioClip[] breakFreeSFX;
    public AudioClip[] bittenSFX;

    public static float SpeedMultiplier => instance != null ? instance.CurrentSpeedMultiplier : 1f;
    /// <summary>Weight of the CloseCombat reaction layer (full body): the melee pipe's arm re-attach steps aside under it.</summary>
    public static float LayerWeight => instance != null ? instance.layerWeight : 0f;
    public static Vector2 AimSway => instance != null ? instance.currentSway : Vector2.zero;
    /// <summary>The dodge's velocity while it's on (PlayerMeleeWeapon moves him with it when the pipe is up, so the strafe
    /// steps play); zero otherwise.</summary>
    public static Vector3 DodgeVelocity => instance != null ? instance.dodgeVelocity + instance.knockVelocity : Vector3.zero;

    public bool IsGrabbed => grabber != null;
    public bool EscapeComplete => grabber != null && presses >= pressesNeeded;
    public bool IsInvulnerable => (roll != null && roll.IsInvulnerable) || Time.time - dodgeStart < dodgeIFrames;
    public bool IsDodging => Time.time < dodgeEnd;
    public bool IsKnockedBack => Time.time < knockEnd;
    public bool CanBeGrabbed => player != null && !player.IsDead() && grabber == null && !player.isScared;
    public bool IsShoving => Time.time < shoveEnd;

    Player player;
    PlayerRoll roll;
    Animator animator;
    int layer = -1;
    float layerWeight, layerTarget;
    float slowUntil, slowAmount;
    float swayUntil, swayStart;
    Vector2 currentSway;
    Transform vcam;
    Quaternion vcamRest;
    bool swayingCam;
    float nextShove, shoveEnd;
    float dodgeStart = -99f, dodgeEnd, nextDodge;
    Vector3 dodgeDir, dodgeVelocity;
    Vector3 knockDir, knockVelocity;
    float knockStart, knockEnd, knockDist;
    CinemachineVirtualCamera grabCam;
    CinemachineBrain brain;
    CinemachineBlendDefinition savedBlend;
    bool blendSaved, grabCamOn;
    float grabCamSide = 1f, grabCamStart, restoreBlendAt = -1f;
    Transform khalidHead, grabberHead;
    PlayerStamina stamina;
    InputAction moveAction;

    QuestionerEnemy grabber;
    int presses, pressesNeeded;
    float grabStart, grabWindow;

    GameObject promptRoot;
    TextMeshProUGUI promptText;
    Image promptFill;

    float CurrentSpeedMultiplier
    {
        get
        {
            if (IsGrabbed || IsShoving || IsDodging || IsKnockedBack) return 0f;
            return Time.time < slowUntil ? 1f - slowAmount : 1f;
        }
    }

    void Awake()
    {
        instance = this;
        player = GetComponent<Player>();
        roll = GetComponent<PlayerRoll>();
        stamina = GetComponent<PlayerStamina>();
        animator = player != null ? player.animator : GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        if (shoveAction != null) shoveAction.action.Enable();
        if (dodgeAction != null) dodgeAction.action.Enable();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (layer < 0 && animator != null) layer = animator.GetLayerIndex(layerName);
        if (player == null) return;

        if (player.IsDead() && IsGrabbed) EndGrab(false);

        bool pressed = shoveAction != null && shoveAction.action.WasPressedThisFrame() && !GameManager.IsPaused;
        if (IsGrabbed)
        {
            if (pressed)
            {
                presses++;
                if (presses % 2 == 1) Play(struggleSFX, 0.7f);
            }
            UpdatePrompt();
        }
        else if (pressed) TryShove();

        if (dodgeAction != null && dodgeAction.action.WasPressedThisFrame() && !GameManager.IsPaused) TryDodge();
        UpdateDodge();
        UpdateSway();

        if (layer >= 0)
        {
            layerWeight = Mathf.MoveTowards(layerWeight, layerTarget, Time.deltaTime / Mathf.Max(0.01f, layerBlend));
            animator.SetLayerWeight(layer, layerWeight);
            if (layerTarget > 0f && !IsGrabbed && Time.time > shoveEnd && Time.time > reactionEnd) layerTarget = 0f;
        }
    }

    float reactionEnd;

    // ---------------- dodge ----------------

    void TryDodge()
    {
        if (Time.time < nextDodge || IsGrabbed || IsShoving || IsDodging) return;
        if (player.IsDead() || player.isScared || player.pauseMovement || player.isAttacking || PlayerQuickHeal.Using) return;
        if (PlayerMeleeWeapon.Attacking) return;
        if (player.playerWeaponSystem != null && player.playerWeaponSystem.isReloading) return;
        if (stamina != null && stamina.Stamina < dodgeStamina) return;
        if (stamina != null) stamina.UseStamina(dodgeStamina);

        if (moveAction == null && player.playerMovement != null) moveAction = player.playerMovement.MoveAction;
        Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        // tank controls: with the pipe up he strafes, so sideways input dodges sideways; otherwise forward / back only
        Vector3 dir = input.sqrMagnitude < 0.04f ? -transform.forward
            : (PlayerMeleeWeapon.Ready ? transform.right * input.x + transform.forward * input.y : transform.forward * Mathf.Sign(input.y == 0 ? -1 : input.y));
        dir.y = 0f;
        dodgeDir = dir.normalized;
        dodgeStart = Time.time;
        dodgeEnd = Time.time + dodgeTime;
        nextDodge = dodgeEnd + dodgeCooldown;
        Play(shoveSFX, 0.35f);
    }

    void UpdateDodge()
    {
        // a Questioner's hit shoving him back (decelerating over knockTime)
        if (IsKnockedBack)
        {
            float u = Mathf.Clamp01((Time.time - knockStart) / Mathf.Max(0.01f, knockEnd - knockStart));
            knockVelocity = knockDir * (knockDist / (knockEnd - knockStart) * 2f * (1f - u));
            if (!PlayerMeleeWeapon.Ready && player.controller != null && player.controller.enabled)
                player.controller.Move(knockVelocity * Time.deltaTime);
        }
        else knockVelocity = Vector3.zero;

        if (!IsDodging) { dodgeVelocity = Vector3.zero; return; }
        // fast out, easing off (distance = dodgeDistance)
        float t = Mathf.Clamp01((Time.time - dodgeStart) / dodgeTime);
        float speed = dodgeDistance / dodgeTime * 2f * (1f - t);
        dodgeVelocity = dodgeDir * speed;
        if (!PlayerMeleeWeapon.Ready && player.controller != null && player.controller.enabled)
            player.controller.Move(dodgeVelocity * Time.deltaTime);
    }

    /// <summary>An enemy hit shoves him back `distance` m over `time` s (his own movement is held meanwhile).</summary>
    public void Knockback(Vector3 dir, float distance, float time)
    {
        if (IsGrabbed || player.IsDead()) return;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        knockDir = dir.normalized;
        knockDist = distance;
        knockStart = Time.time;
        knockEnd = Time.time + Mathf.Max(0.05f, time);
    }

    // ---------------- shove ----------------

    void TryShove()
    {
        if (Time.time < nextShove) return;
        if (player.IsDead() || player.isRolling || player.isScared || player.pauseMovement || PlayerQuickHeal.Using) return;
        if (PlayerMeleeWeapon.instance != null && PlayerMeleeWeapon.Ready) return;      // the pipe is up: swing instead
        nextShove = Time.time + shoveCooldown;
        shoveEnd = Time.time + shoveTime;
        PlayReaction("Shove", shoveTime);
        Play(shoveSFX, 0.8f);

        foreach (var q in Object.FindObjectsByType<QuestionerEnemy>(FindObjectsSortMode.None))
        {
            if (q.health == null || q.health.IsDead || q.stateMachine.CurrentState != q.combatState) continue;
            Vector3 to = q.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > shoveRange) continue;
            if (to.magnitude > 0.5f && Vector3.Angle(transform.forward, to) > shoveArc * 0.5f) continue;
            q.combatState.OnShoved();
        }
    }

    // ---------------- grabbed ----------------

    public void BeginGrab(QuestionerEnemy q, float window, int needed)
    {
        grabber = q;
        presses = 0;
        pressesNeeded = Mathf.Max(1, needed);
        grabStart = Time.time;
        grabWindow = window;
        player.isGrabbed = true;
        if (player.controller != null) player.controller.Move(Vector3.zero);
        PlayReaction("Grabbed", 99f);
        ShowPrompt(true);
        StartGrabCam(q);
    }

    /// <summary>Keeps him where the Questioner holds him (in front of the split mouth), facing it.</summary>
    public void HoldAt(Vector3 position, Transform holder)
    {
        if (grabber == null) return;
        position.y = transform.position.y;
        Vector3 face = holder.position - position;
        face.y = 0f;
        var cc = player.controller;
        Vector3 delta = position - transform.position;
        if (cc != null && cc.enabled) cc.Move(Vector3.ClampMagnitude(delta, 8f * Time.deltaTime));
        else transform.position = Vector3.MoveTowards(transform.position, position, 8f * Time.deltaTime);
        if (face.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), 720f * Time.deltaTime);
    }

    public void EndGrab(bool pushedOff)
    {
        if (grabber == null) return;
        grabber = null;
        player.isGrabbed = false;
        ShowPrompt(false);
        StopGrabCam();
        if (pushedOff)
        {
            Play(breakFreeSFX, 0.9f);
            PlayReaction("BreakFree", 0.6f);
        }
        else if (Time.time > reactionEnd) layerTarget = 0f;
        if (player.playerWeaponSystem != null) player.playerWeaponSystem.SyncCameraYaw();
    }

    public void OnBitten()
    {
        Play(bittenSFX, 1f);
        PlayReaction("Bitten", 0.9f);
    }

    // ---------------- grab camera ----------------

    static Transform FindBone(Transform root, params string[] names)
    {
        foreach (var n in names)
            foreach (var t in root.GetComponentsInChildren<Transform>())
                if (t.name == n) return t;
        return null;
    }

    void StartGrabCam(QuestionerEnemy q)
    {
        var main = Camera.main;
        if (main == null) return;
        brain = main.GetComponent<CinemachineBrain>();
        if (brain == null) return;
        if (grabCam == null)
        {
            var go = new GameObject("GrabCam");
            grabCam = go.AddComponent<CinemachineVirtualCamera>();
        }
        var lens = grabCam.m_Lens;
        lens.FieldOfView = grabCamFov;
        lens.NearClipPlane = 0.05f;
        grabCam.m_Lens = lens;
        if (khalidHead == null) khalidHead = FindBone(transform, "head.x", "Head");
        grabberHead = FindBone(q.transform, "Head");
        // on the side his grabbed struggle turns his face to (away from the mouth): his face, not the back of his head
        grabCamSide = -1f;
        if (!blendSaved) { savedBlend = brain.m_DefaultBlend; blendSaved = true; }
        brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, grabCamBlend);
        restoreBlendAt = -1f;
        grabCamStart = Time.time;
        grabCamOn = true;
        PlaceGrabCam();
        grabCam.Priority = 1000;
        grabCam.gameObject.SetActive(true);
    }

    void StopGrabCam()
    {
        if (!grabCamOn) return;
        grabCamOn = false;
        if (grabCam != null) grabCam.Priority = -100;
        restoreBlendAt = Time.time + grabCamBlend + 0.1f;     // keep the quick blend for the cut back
    }

    void PlaceGrabCam()
    {
        if (grabCam == null || grabber == null) return;
        Vector3 a = khalidHead != null ? khalidHead.position : transform.position + Vector3.up * 1.65f;
        Vector3 b = grabberHead != null ? grabberHead.position : grabber.transform.position + Vector3.up * 1.8f;
        Vector3 mid = (a + b) * 0.5f;
        Vector3 axis = b - a; axis.y = 0f;
        if (axis.sqrMagnitude < 0.0001f) axis = transform.forward;
        axis.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, axis) * grabCamSide;
        // creeps in on them while he holds on
        float push = Mathf.Clamp01((Time.time - grabCamStart) / 1.8f) * grabCamPushIn;
        Vector3 pos = mid + side * (grabCamDistance - push) + axis * 0.15f + Vector3.up * 0.05f;      // near profile, nudged toward the Questioner so Khalid's face turns to camera
        grabCam.transform.position = pos;
        grabCam.transform.rotation = Quaternion.LookRotation(mid + Vector3.down * 0.04f - pos, Vector3.up);
    }

    void LateUpdate()
    {
        if (grabCamOn) PlaceGrabCam();
        if (restoreBlendAt > 0f && Time.time >= restoreBlendAt)
        {
            restoreBlendAt = -1f;
            if (brain != null && blendSaved) brain.m_DefaultBlend = savedBlend;
            blendSaved = false;
            if (grabCam != null) grabCam.gameObject.SetActive(false);
        }
    }

    // ---------------- scream ----------------

    public void ApplyScream(float slow, float duration)
    {
        slowAmount = slow;
        slowUntil = Time.time + duration;
        swayStart = Time.time;
        swayUntil = Time.time + duration;
    }

    void UpdateSway()
    {
        float left = swayUntil - Time.time;
        if (left > 0f)
        {
            float env = Mathf.Clamp01(left / 0.3f) * Mathf.Clamp01((Time.time - swayStart) / 0.1f);
            float t = Time.time * 2.3f;
            currentSway = new Vector2(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * swayAmount * env;
        }
        else currentSway = Vector2.zero;

        if (vcam == null && CameraSystem.Instance != null)
        {
            var v = CameraSystem.Instance.GetComponentInChildren<CinemachineVirtualCamera>(true);
            if (v != null) vcam = v.transform;
        }
        if (vcam == null) return;
        if (currentSway != Vector2.zero)
        {
            if (!swayingCam) { vcamRest = vcam.localRotation; swayingCam = true; }
            float k = swayCameraDegrees / Mathf.Max(0.001f, swayAmount);
            vcam.localRotation = vcamRest * Quaternion.Euler(-currentSway.y * k, currentSway.x * k, currentSway.x * k * 0.5f);
        }
        else if (swayingCam)
        {
            vcam.localRotation = vcamRest;
            swayingCam = false;
        }
    }

    // ---------------- animation / audio ----------------

    void PlayReaction(string state, float duration)
    {
        if (animator == null || layer < 0) return;
        int hash = Animator.StringToHash(state);
        if (!animator.HasState(layer, hash)) return;
        animator.CrossFadeInFixedTime(hash, 0.08f, layer, 0f);
        layerTarget = 1f;
        reactionEnd = Time.time + duration;
    }

    void Play(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0) return;
        var c = clips[Random.Range(0, clips.Length)];
        if (c != null && SoundEffectManager.instance != null) SoundEffectManager.instance.PlaySFX(c, volume, true);
    }

    // ---------------- escape prompt ----------------

    void ShowPrompt(bool on)
    {
        if (on && promptRoot == null) BuildPrompt();
        if (promptRoot != null) promptRoot.SetActive(on);
        if (on) UpdatePrompt();
    }

    void UpdatePrompt()
    {
        if (promptRoot == null) return;
        string key = shoveAction != null ? shoveAction.action.GetBindingDisplayString(0) : "V";
        float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 22f);
        promptText.text = "PUSH HIM OFF  [" + key + "]";
        promptText.transform.localScale = Vector3.one * pulse;
        promptFill.fillAmount = Mathf.Clamp01((float)presses / pressesNeeded);
        float timeLeft = 1f - Mathf.Clamp01((Time.time - grabStart) / Mathf.Max(0.01f, grabWindow));
        promptFill.color = Color.Lerp(new Color(0.8f, 0.1f, 0.05f, 0.9f), new Color(0.95f, 0.9f, 0.8f, 0.9f), timeLeft);
    }

    void BuildPrompt()
    {
        promptRoot = new GameObject("GrabPrompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = promptRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = promptRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        promptRoot.transform.SetParent(transform, false);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(promptRoot.transform, false);
        promptText = textGo.AddComponent<TextMeshProUGUI>();
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.fontSize = 46;
        promptText.color = new Color(0.95f, 0.92f, 0.85f);
        promptText.outlineWidth = 0.25f;
        var rt = (RectTransform)textGo.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.27f);
        rt.sizeDelta = new Vector2(900, 80);

        var barBg = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
        barBg.transform.SetParent(promptRoot.transform, false);
        barBg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        var bt = (RectTransform)barBg.transform;
        bt.anchorMin = bt.anchorMax = new Vector2(0.5f, 0.22f);
        bt.sizeDelta = new Vector2(420, 16);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(barBg.transform, false);
        promptFill = fill.GetComponent<Image>();
        promptFill.type = Image.Type.Filled;
        promptFill.fillMethod = Image.FillMethod.Horizontal;
        promptFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
        var ft = (RectTransform)fill.transform;
        ft.anchorMin = Vector2.zero;
        ft.anchorMax = Vector2.one;
        ft.offsetMin = new Vector2(2, 2);
        ft.offsetMax = new Vector2(-2, -2);
        promptRoot.SetActive(false);
    }
}
