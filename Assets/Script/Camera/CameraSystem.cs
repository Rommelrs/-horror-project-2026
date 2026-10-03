using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraSystem : MonoBehaviour
{
    public static CameraSystem Instance { get; private set; }

    [Header("Setup")]
    [SerializeField] Transform m_CameraTransform;
    [SerializeField] PlayerMovement m_PlayerMovement;

    [Header("Follow")]
    [SerializeField] Transform followTarget;
    [SerializeField] float followSpeed = 1.0f;

    [Header("LookAt")]
    [SerializeField] Transform lookAtTarget;
    [SerializeField] float lookAtSpeed = 1.0f;

    [Header("Tank Camera Behavior")]
    [Tooltip("How far the player's facing has to drift from the current camera angle before the camera starts catching up. Keeps the camera from constantly self-correcting on every small turn.")]
    [SerializeField] float rotationEngageAngle = 12f;
    [Tooltip("If the CHARACTER's own facing snaps by more than this in a single frame (the dedicated quick-180 action), the camera cuts instantly instead of swinging through it. Ordinary sustained turning never triggers this, no matter how far the camera ends up lagging behind.")]
    [SerializeField] float instantCutAngle = 150f;
    [Tooltip("Roughly how long (seconds) the camera takes to ease into catching up - gives it real weight/momentum instead of a mechanical constant-speed correction. Larger = more visible independent swing.")]
    [SerializeField] float tankCameraRotationSmoothTime = 0.85f;
    [Tooltip("Hard cap on turn speed while catching up - must stay above the character's own turn speed (150 deg/sec) or the camera will never fully close the gap during a sustained turn.")]
    [SerializeField] float tankCameraMaxDegreesPerSecond = 200f;
    [Tooltip("How long (seconds) the camera's position takes to catch up to the player - gives it independent inertia instead of being rigidly glued to the player's position.")]
    [SerializeField] float tankCameraPositionSmoothTime = 0.55f;
    [SerializeField] bool showDebugOverlay = false;
    [Tooltip("Key that snaps the camera back behind the player on demand.")]
    [SerializeField] Key recenterKey = Key.F8;

    [Header("Continuous-Turn Front Swing")]
    [Tooltip("How many seconds of continuous turning in one direction before the camera fully swings around to face the player from the front, when the player is standing still (not moving forward). Should be quick, but not instant.")]
    [SerializeField] float standingTurnTimeToFront = 0.9f;
    [Tooltip("How many seconds of continuous turning before the front swing triggers while sprinting at full speed - deliberately much longer, so casually turning while sprinting doesn't yank the camera around.")]
    [SerializeField] float sprintTurnTimeToFront = 3.5f;
    [Tooltip("How long (seconds) the front/behind blend takes to ease in and out - gives it a natural deceleration instead of stopping dead.")]
    [SerializeField] float frontBlendSmoothTime = 0.5f;
    [Tooltip("Minimum turn input required to count as 'sustaining a turn' (ignores tiny stick drift).")]
    [SerializeField] float sustainedTurnInputThreshold = 0.3f;
    [Tooltip("Extra distance the camera pulls back (added on top of the normal distance) as it swings toward facing the player head-on, so the front-facing reveal has proper breathing room instead of feeling cramped.")]
    [SerializeField] float frontSwingExtraDistance = 2.2f;

    [Header("Free Camera (optional)")]
    [Tooltip("Key that switches between the classic tank camera and the free (mouse-controlled) camera. The choice is remembered between sessions.")]
    [SerializeField] Key toggleFreeCameraKey = Key.C;
    [Tooltip("Base look speed multiplier in free camera mode. The 'Free Camera Speed' slider in the Options menu multiplies this. Independent of the aim sensitivity setting.")]
    [SerializeField] float freeCameraSensitivity = 1f;
    [Tooltip("Lowest the camera can look up (degrees, negative = looking up).")]
    [SerializeField] float freePitchMin = -20f;
    [Tooltip("Highest the camera can look down (degrees).")]
    [SerializeField] float freePitchMax = 55f;
    [Tooltip("How long (seconds) the camera takes to swing back behind the player when re-centering.")]
    [SerializeField] float recenterSmoothTime = 0.25f;
    [Tooltip("After aiming ends, ease the free camera back behind the player.")]
    [SerializeField] bool recenterAfterAiming = true;

    public const string FreeCameraPrefKey = "FreeCameraMode";
    public const string FreeCameraSpeedPrefKey = "FreeCameraSpeed";

    /// <summary>True while the free (mouse-controlled) camera is selected instead of the classic tank camera.</summary>
    public bool FreeCameraEnabled { get; private set; }

    /// <summary>User look-speed multiplier for the free camera (Options menu slider).</summary>
    public float FreeCameraSpeed { get; private set; } = 1f;

    /// <summary>Raised whenever the free camera is switched on or off (hotkey or Options menu).</summary>
    public event System.Action<bool> FreeCameraChanged;

    Transform pitchRoot;          // RotationRoot - holds the camera's tilt
    float defaultPitch;           // tilt of the classic camera
    float freeYaw;
    float freePitch;
    bool freeSynced;
    bool recentering;
    bool wasAiming;
    bool pitchDirty;
    float recenterYawVelocity;
    float recenterPitchVelocity;
    float pitchRestoreVelocity;

    float camYawVelocity;
    Vector3 camPositionVelocity;
    Quaternion lastPlayerRotation;
    bool hasLastPlayerRotation;
    float sustainedTurnDirection;
    float sustainedTurnTimer;
    float frontBlend;
    float frontBlendVelocity;

    // Debug-only, read by the on-screen overlay
    float debugAngleLag;
    float debugPositionLag;
    string debugLastEvent = "";

    [Header("Camera Collision")]
    [SerializeField] Transform m_CameraPositionOffset;
    [SerializeField] float collisionRadius = 0.2f;
    [SerializeField] float zoomSmoothTime = 0.08f;
    [SerializeField] LayerMask cameraCollisionLayer;
    [SerializeField] float zoomMin;
    [SerializeField] float zoomMax;
    [SerializeField] float collisionOffset = 0.15f;
    float zoomVelocity;

    [Header("Wall Avoidance")]
    [Tooltip("Keep the camera clear of walls: it checks the whole line from the player to the camera (including the camera tilt) and the space around the camera itself, so it can't end up inside geometry.")]
    [SerializeField] bool wallAvoidanceEnabled = true;
    [Tooltip("How much free space (metres) must surround the camera. Roughly the size of the camera's near-clip box - raise it if you ever see through a wall's edge.")]
    [SerializeField] float cameraClearanceRadius = 0.35f;
    [Tooltip("How slowly (seconds) the camera eases back out once the wall is no longer in the way. Pulling IN is always instant so it never clips.")]
    [SerializeField] float zoomOutSmoothTime = 0.3f;
    [Tooltip("When the classic camera gets squeezed this much (0 = not at all, 1 = pushed all the way in), it swings sideways towards the side with more room.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] float wallSqueezeThreshold = 0.35f;
    [Tooltip("How far (degrees) the classic camera is allowed to swing sideways to find room. 0 turns the sideways swing off (the camera then only moves closer).")]
    [SerializeField] float wallSwingAngle = 40f;
    [Tooltip("How long (seconds) the sideways swing takes to ease in and out.")]
    [SerializeField] float wallSwingSmoothTime = 0.6f;

    [Header("Player Visibility")]
    [Tooltip("Hide the player's body (it still casts a shadow) when the camera is squeezed so close that it would be inside the model.")]
    [SerializeField] bool hidePlayerWhenCameraClose = true;
    [Tooltip("The body is hidden once the camera is closer than this (metres) to the player's head.")]
    [SerializeField] float hidePlayerDistance = 1.3f;
    [Tooltip("The body reappears once the camera is farther than this. Keep it a bit larger than the hide distance so it doesn't flicker.")]
    [SerializeField] float showPlayerDistance = 1.55f;

    float wallYaw;           // sideways swing currently applied to the camera (degrees)
    float wallYawVelocity;
    bool wallSwingActive;

    struct CachedRenderer
    {
        public Renderer renderer;
        public UnityEngine.Rendering.ShadowCastingMode shadowMode;
    }
    readonly List<CachedRenderer> playerRenderers = new List<CachedRenderer>();
    bool playerHidden;

    Vector3 targetFollowPosition;
    Vector2 moveInput;
    bool wantsRecenter;

    CameraFixedZone activeZone;
    float lastZoneSwitchTime = -999f;
    const float zoneSwitchCooldown = 0.3f;

    private void Awake()
    {
        Instance = this;
        FreeCameraEnabled = PlayerPrefs.GetInt(FreeCameraPrefKey, 0) == 1;
        FreeCameraSpeed = Mathf.Max(0.05f, PlayerPrefs.GetFloat(FreeCameraSpeedPrefKey, 1f));
    }

    private void Start()
    {
        if (m_CameraPositionOffset != null)
        {
            pitchRoot = m_CameraPositionOffset.parent;
            if (pitchRoot != null)
                defaultPitch = NormalizeAngle(pitchRoot.localEulerAngles.x);
        }
        freePitch = defaultPitch;

        CachePlayerRenderers();

        if (Player.instance)
            Player.instance.OnPlayerTelported.AddListener(OnPlayerTeleported);

        if (m_PlayerMovement != null)
        {
            lastPlayerRotation = m_PlayerMovement.transform.rotation;
            hasLastPlayerRotation = true;
        }
    }

    private void OnDestroy()
    {
        SetPlayerHidden(false);

        if (Player.instance)
            Player.instance.OnPlayerTelported.RemoveListener(OnPlayerTeleported);
    }

    void OnPlayerTeleported()
    {
        //Vector3 localPos = m_CameraPositionOffset.localPosition;
        //localPos.z = zoomMin;
        //m_CameraPositionOffset.localPosition = localPos;

        targetFollowPosition = followTarget.position;
        m_CameraTransform.position = targetFollowPosition;

        //Update rotation instantly
        m_CameraTransform.rotation = Quaternion.LookRotation(lookAtTarget.forward, Vector3.up);

        // Free camera picks up from wherever the teleport left the camera
        freeSynced = false;
        recentering = false;

        // Start fresh after a teleport: no leftover sideways swing, and the camera isn't pinned to the old spot's zoom
        wallYaw = 0f;
        wallYawVelocity = 0f;
        wallSwingActive = false;
        zoomVelocity = 0f;
    }

    private void LateUpdate()
    {
        var keyboard = Keyboard.current;
        wantsRecenter = keyboard != null && keyboard[recenterKey].wasPressedThisFrame;
        moveInput = m_PlayerMovement.GetMoveInput;

        if (keyboard != null && keyboard[toggleFreeCameraKey].wasPressedThisFrame && !GameManager.IsPaused && Time.timeScale > 0f)
            SetFreeCamera(!FreeCameraEnabled, true);

        if (activeZone != null)
        {
            freeSynced = false;
            wallYaw = 0f;
            wallYawVelocity = 0f;
            wallSwingActive = false;
            RestoreClassicPitch();
            SetPlayerHidden(false);
            HandleFixedZoneCamera();
        }
        else
        {
            HandlePosition();

            if (FreeCameraEnabled)
            {
                HandleFreeRotation();
            }
            else
            {
                freeSynced = false;
                RestoreClassicPitch();
                HandleRotation();
            }

            CheckCameraCollision();
        }
    }

    /// <summary>Switches between the classic tank camera (false) and the free mouse camera (true).</summary>
    public void SetFreeCamera(bool enabled, bool showMessage = false)
    {
        if (FreeCameraEnabled == enabled) return;

        FreeCameraEnabled = enabled;
        freeSynced = false;
        recentering = false;

        PlayerPrefs.SetInt(FreeCameraPrefKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        FreeCameraChanged?.Invoke(enabled);

        if (showMessage && MessageHandler.instance != null)
            MessageHandler.instance.ShowMessage(enabled ? "Free camera: ON (middle mouse to re-center)" : "Free camera: OFF (classic camera)");
    }

    /// <summary>Sets the free camera look-speed multiplier (saved).</summary>
    public void SetFreeCameraSpeed(float speed)
    {
        FreeCameraSpeed = Mathf.Max(0.05f, speed);
        PlayerPrefs.SetFloat(FreeCameraSpeedPrefKey, FreeCameraSpeed);
    }

    bool CanReadFreeLook(PlayerWeaponSystem weapon)
    {
        if (weapon == null || Player.instance == null) return false;
        if (GameManager.IsPaused || Time.timeScale <= 0f) return false;
        if (Player.instance.pauseMovement || Player.instance.IsDead()) return false;
        if (weapon.isAiming) return false;
        if (MapLocationReveal.IsSequenceActive) return false;
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) return false;
        if (InventoryUI.instance != null && InventoryUI.instance.InventoryIsActive()) return false;
        if (ItemInspectionHandler.instance != null && ItemInspectionHandler.instance.InspectionMenuIsActive()) return false;
        if (EyePeakHandler.instance != null && EyePeakHandler.instance.IsEyePeakActivated()) return false;
        if (LevelManager.instance != null && (LevelManager.instance.isGameOver || LevelManager.instance.isGameWon)) return false;
        return true;
    }

    // Free camera: the mouse (or right stick) orbits the camera around the player. The player's
    // tank-style movement is unaffected. Middle mouse / right-stick click / F8 swings the camera
    // back behind the player (eased, and interrupted by any new look input), and it also eases
    // back behind the player after aiming ends.
    void HandleFreeRotation()
    {
        // Keep the classic camera's bookkeeping current so switching back never causes a false "instant cut"
        lastPlayerRotation = m_PlayerMovement.transform.rotation;
        hasLastPlayerRotation = true;
        frontBlend = 0f;
        frontBlendVelocity = 0f;
        sustainedTurnTimer = 0f;
        sustainedTurnDirection = 0f;
        camYawVelocity = 0f;
        debugLastEvent = "FREE CAMERA";

        PlayerWeaponSystem weapon = Player.instance != null ? Player.instance.playerWeaponSystem : null;

        if (!freeSynced)
        {
            // Carry over any sideways wall swing so the view doesn't jump when switching to free mode
            freeYaw = m_CameraTransform.eulerAngles.y + wallYaw;
            freePitch = pitchRoot != null ? NormalizeAngle(pitchRoot.localEulerAngles.x) : defaultPitch;
            wallYaw = 0f;
            wallYawVelocity = 0f;
            wallSwingActive = false;
            freeSynced = true;
        }

        bool aiming = weapon != null && weapon.isAiming;
        if (recenterAfterAiming && wasAiming && !aiming)
            recentering = true;
        wasAiming = aiming;

        var mouse = Mouse.current;
        var gamepad = Gamepad.current;
        bool recenterPressed = wantsRecenter
            || (mouse != null && mouse.middleButton.wasPressedThisFrame)
            || (gamepad != null && gamepad.rightStickButton.wasPressedThisFrame);

        bool canLook = CanReadFreeLook(weapon);

        if (canLook && recenterPressed)
            recentering = true;

        if (canLook)
        {
            Vector2 look = weapon.ReadLookInput();
            if (look.sqrMagnitude > 0.0001f)
            {
                recentering = false;
                float scale = Time.deltaTime * FreeCameraSpeed * freeCameraSensitivity;
                freeYaw += look.x * scale;
                freePitch += look.y * scale;
            }
        }

        if (recentering)
        {
            float targetYaw = lookAtTarget.eulerAngles.y;
            freeYaw = Mathf.SmoothDampAngle(freeYaw, targetYaw, ref recenterYawVelocity, recenterSmoothTime);
            freePitch = Mathf.SmoothDampAngle(freePitch, defaultPitch, ref recenterPitchVelocity, recenterSmoothTime);

            if (Mathf.Abs(Mathf.DeltaAngle(freeYaw, targetYaw)) < 0.5f && Mathf.Abs(Mathf.DeltaAngle(freePitch, defaultPitch)) < 0.5f)
            {
                freeYaw = targetYaw;
                freePitch = defaultPitch;
                recentering = false;
            }
        }
        else
        {
            recenterYawVelocity = 0f;
            recenterPitchVelocity = 0f;
        }

        freeYaw = Mathf.Repeat(freeYaw, 360f);
        freePitch = Mathf.Clamp(freePitch, freePitchMin, freePitchMax);

        m_CameraTransform.rotation = Quaternion.Euler(0f, freeYaw, 0f);
        if (pitchRoot != null)
        {
            pitchRoot.localRotation = Quaternion.Euler(freePitch, 0f, 0f);
            pitchDirty = true;
        }
    }

    // After leaving free camera mode (or entering a fixed camera zone) the tilt eases back to the
    // classic camera's angle.
    void RestoreClassicPitch()
    {
        if (!pitchDirty || pitchRoot == null) return;

        float current = NormalizeAngle(pitchRoot.localEulerAngles.x);
        bool pitchDone = Mathf.Abs(Mathf.DeltaAngle(current, defaultPitch)) < 0.05f;

        if (pitchDone && Mathf.Abs(wallYaw) < 0.05f)
        {
            wallYaw = 0f;
            pitchRoot.localRotation = Quaternion.Euler(defaultPitch, 0f, 0f);
            pitchDirty = false;
            pitchRestoreVelocity = 0f;
            return;
        }

        current = pitchDone ? defaultPitch : Mathf.SmoothDampAngle(current, defaultPitch, ref pitchRestoreVelocity, 0.3f);

        // Keep any sideways wall swing that is currently applied (it eases out on its own)
        pitchRoot.localRotation = Quaternion.Euler(current, wallYaw, 0f);
    }

    static float NormalizeAngle(float angle)
    {
        return Mathf.DeltaAngle(0f, angle);
    }

    // Called by CameraFixedZone triggers when the player enters/exits a designer-placed
    // fixed camera angle (classic RE/Silent Hill style semi-fixed room camera).
    public void EnterFixedZone(CameraFixedZone zone)
    {
        if (zone == activeZone) return;
        if (Time.time - lastZoneSwitchTime < zoneSwitchCooldown) return; // avoid rapid back-and-forth switching

        activeZone = zone;
        lastZoneSwitchTime = Time.time;

        if (zone.cutOnEnter)
        {
            m_CameraTransform.position = zone.cameraAnchor.position;
            m_CameraTransform.rotation = zone.cameraAnchor.rotation;
        }
    }

    public void ExitFixedZone(CameraFixedZone zone)
    {
        if (activeZone == zone)
            activeZone = null;
    }

    void HandleFixedZoneCamera()
    {
        Transform anchor = activeZone.cameraAnchor;
        m_CameraTransform.position = Vector3.Lerp(m_CameraTransform.position, anchor.position, followSpeed * Time.deltaTime);
        m_CameraTransform.rotation = Quaternion.Lerp(m_CameraTransform.rotation, anchor.rotation, lookAtSpeed * Time.deltaTime);
    }

    void HandlePosition()
    {
        // SmoothDamp gives the camera its own independent momentum instead of being
        // rigidly glued to the player's position every frame.
        targetFollowPosition = followTarget.position;
        m_CameraTransform.position = Vector3.SmoothDamp(m_CameraTransform.position, targetFollowPosition, ref camPositionVelocity, tankCameraPositionSmoothTime);
        debugPositionLag = Vector3.Distance(m_CameraTransform.position, targetFollowPosition);
    }

    void HandleRotation()
    {
        Quaternion rotation = Quaternion.LookRotation(lookAtTarget.forward, Vector3.up);
        HandleTankCameraRotation(rotation);
    }

    // Tank mode's camera deliberately behaves like it has real weight/momentum rather than
    // being glued to the player: it only starts catching up once the player's facing has
    // drifted meaningfully, it eases in/out via SmoothDampAngle instead of a mechanical
    // constant-speed correction, it settles and stops the instant the player stops turning,
    // and it only ever cuts instantly for the CHARACTER's own instantaneous flip (the
    // dedicated quick-180 action) - an ordinary sustained turn always swings smoothly
    // through the full arc, however far the camera ends up lagging behind, which is what
    // lets it end up beside or even in front of the player mid-turn instead of always
    // snapping back to directly behind.
    void HandleTankCameraRotation(Quaternion behindRotation)
    {
        UpdateFrontSwingBlend();

        // Blend the camera's target from "behind the player" toward "opposite the player,
        // facing back at them" the longer they sustain a turn in one direction - this is
        // what lets the camera swing all the way around to lead in front during a long turn
        // instead of only ever lagging partway there.
        Quaternion frontRotation = Quaternion.LookRotation(-lookAtTarget.forward, Vector3.up);
        Quaternion desiredRotation = Quaternion.Slerp(behindRotation, frontRotation, frontBlend);

        Transform playerTransform = m_PlayerMovement.transform;
        float playerFrameDelta = hasLastPlayerRotation ? Quaternion.Angle(lastPlayerRotation, playerTransform.rotation) : 0f;
        lastPlayerRotation = playerTransform.rotation;
        hasLastPlayerRotation = true;

        if (wantsRecenter)
        {
            frontBlend = 0f;
            sustainedTurnTimer = 0f;
            sustainedTurnDirection = 0f;
            m_CameraTransform.rotation = behindRotation;
            camYawVelocity = 0f;
            debugAngleLag = 0f;
            debugLastEvent = "RECENTER (instant snap)";
            return;
        }

        if (playerFrameDelta > instantCutAngle)
        {
            m_CameraTransform.rotation = desiredRotation;
            camYawVelocity = 0f;
            debugAngleLag = 0f;
            debugLastEvent = "INSTANT CUT (player flipped " + playerFrameDelta.ToString("F0") + " deg in 1 frame)";
            return;
        }

        float angleDelta = Quaternion.Angle(m_CameraTransform.rotation, desiredRotation);
        debugAngleLag = angleDelta;
        bool hasMoveInput = moveInput.magnitude > 0.1f;

        if (hasMoveInput && angleDelta > rotationEngageAngle)
        {
            float currentYaw = m_CameraTransform.eulerAngles.y;
            float desiredYaw = desiredRotation.eulerAngles.y;
            float newYaw = Mathf.SmoothDampAngle(currentYaw, desiredYaw, ref camYawVelocity, tankCameraRotationSmoothTime, tankCameraMaxDegreesPerSecond);

            Vector3 euler = m_CameraTransform.eulerAngles;
            euler.y = newYaw;
            m_CameraTransform.eulerAngles = euler;
            debugLastEvent = "catching up (velocity=" + camYawVelocity.ToString("F0") + " deg/s)";
        }
        else
        {
            // Reset velocity so a stale value doesn't cause an overshoot/hiccup next time
            // the camera re-engages, instead of continuing to drift while input is idle.
            camYawVelocity = 0f;
            debugLastEvent = hasMoveInput ? "within deadzone, holding" : "no input, settled";
        }

        debugLastEvent += " | frontBlend=" + frontBlend.ToString("F2");
    }

    // Tracks how long the player has been turning continuously in one direction, and blends
    // frontBlend from 0 (normal, camera behind the player) toward 1 (camera fully swung
    // around, facing back at the player) the longer that sustains. Reversing direction
    // restarts the ramp. Freezes entirely while the player is fully idle, consistent with
    // the rest of the camera settling in place rather than drifting with no input.
    void UpdateFrontSwingBlend()
    {
        bool hasAnyInput = moveInput.magnitude > 0.1f;
        if (!hasAnyInput) return;

        bool turningNow = Mathf.Abs(moveInput.x) > sustainedTurnInputThreshold;

        if (turningNow)
        {
            float dir = Mathf.Sign(moveInput.x);
            if (!Mathf.Approximately(dir, sustainedTurnDirection))
            {
                sustainedTurnDirection = dir;
                sustainedTurnTimer = 0f;
            }
            else
            {
                sustainedTurnTimer += Time.deltaTime;
            }
        }
        else
        {
            sustainedTurnDirection = 0f;
            sustainedTurnTimer = 0f;
        }

        // Scale how long a sustained turn needs to hold before swinging to the front based on
        // how fast the player is currently moving - standing still swings around quickly (but
        // not instantly), while sprinting deliberately takes much longer, so casually tapping
        // a direction while running doesn't yank the camera around.
        float speedRatio = m_PlayerMovement.SprintSpeed > 0f
            ? Mathf.Clamp01(m_PlayerMovement.CurrentSpeed / m_PlayerMovement.SprintSpeed)
            : 0f;
        float effectiveTimeToFront = Mathf.Lerp(standingTurnTimeToFront, sprintTurnTimeToFront, speedRatio);

        float targetBlend = Mathf.Clamp01(sustainedTurnTimer / effectiveTimeToFront);

        // SmoothDamp instead of MoveTowards - MoveTowards moves at a flat constant rate and
        // then halts dead the instant it reaches the target, with zero deceleration, which is
        // exactly what read as a "sudden stop" once the swing finished or fully relaxed back.
        frontBlend = Mathf.SmoothDamp(frontBlend, targetBlend, ref frontBlendVelocity, frontBlendSmoothTime);
        frontBlend = Mathf.Clamp01(frontBlend);
    }

    void OnGUI()
    {
        if (!showDebugOverlay) return;

        GUI.Label(new Rect(10, 40, 500, 20), "Angle lag: " + debugAngleLag.ToString("F1") + " deg (engage at " + rotationEngageAngle + ")");
        GUI.Label(new Rect(10, 60, 500, 20), "Position lag: " + debugPositionLag.ToString("F2") + " units");
        GUI.Label(new Rect(10, 80, 500, 20), "State: " + debugLastEvent);
        GUI.Label(new Rect(10, 100, 500, 20), "Rotation smoothTime=" + tankCameraRotationSmoothTime + " maxSpeed=" + tankCameraMaxDegreesPerSecond);
        GUI.Label(new Rect(10, 120, 500, 20), "Position smoothTime=" + tankCameraPositionSmoothTime);
        GUI.Label(new Rect(10, 140, 500, 20), "Front swing blend: " + frontBlend.ToString("F2") + " (0=behind, 1=facing player from front) sustainedTurnTimer=" + sustainedTurnTimer.ToString("F1"));
        float debugSpeedRatio = m_PlayerMovement.SprintSpeed > 0f ? Mathf.Clamp01(m_PlayerMovement.CurrentSpeed / m_PlayerMovement.SprintSpeed) : 0f;
        GUI.Label(new Rect(10, 160, 500, 20), "CurrentSpeed=" + m_PlayerMovement.CurrentSpeed.ToString("F2") + " speedRatio=" + debugSpeedRatio.ToString("F2") +
            " effectiveTimeToFront=" + Mathf.Lerp(standingTurnTimeToFront, sprintTurnTimeToFront, debugSpeedRatio).ToString("F2"));
    }

    // Keeps the camera out of walls. Instead of one line cast straight back, it checks the whole
    // line from the player's head to where the camera REALLY is (so the camera's tilt and its lag
    // behind the player are accounted for) plus a clearance sphere around the camera itself
    // (so side walls, ceilings and floors count too). Pulling in is instant so it can never
    // clip; easing back out is gradual so grazing a wall doesn't make it jitter. In classic mode,
    // when the camera is being squeezed it also swings sideways towards the side with more room.
    void CheckCameraCollision()
    {
        if (!wallAvoidanceEnabled)
        {
            CheckCameraCollisionLegacy();
            return;
        }

        float offsetY = m_CameraPositionOffset.localPosition.y;
        Vector3 head = followTarget.position + new Vector3(0f, offsetY, 0f);
        float desiredZoom = zoomMax - (frontSwingExtraDistance * frontBlend);

        // Sideways swing (classic camera only - the free camera is under the player's control)
        UpdateWallSwing(head, offsetY, desiredZoom);

        Transform tilt = pitchRoot != null ? pitchRoot : m_CameraTransform;
        float safeZoom = FindSafeZoom(tilt.rotation, tilt.position, head, offsetY, desiredZoom);

        float currentZoom = m_CameraPositionOffset.localPosition.z;
        float newZoom;
        if (safeZoom > currentZoom)
        {
            // Needs to be closer than it is now: do it at once, never let the camera sit inside geometry
            newZoom = safeZoom;
            zoomVelocity = 0f;
        }
        else
        {
            newZoom = Mathf.SmoothDamp(currentZoom, safeZoom, ref zoomVelocity, zoomOutSmoothTime);
        }

        Vector3 localPos = m_CameraPositionOffset.localPosition;
        localPos.z = newZoom;
        m_CameraPositionOffset.localPosition = localPos;

        UpdatePlayerVisibility(head);
    }

    Vector3 CameraPositionAtZoom(Quaternion tiltRotation, Vector3 rootPosition, float offsetY, float zoom)
    {
        return rootPosition + tiltRotation * new Vector3(0f, offsetY, zoom);
    }

    bool IsCameraSpotClear(Vector3 head, Vector3 cameraPosition)
    {
        Vector3 toCamera = cameraPosition - head;
        float distance = toCamera.magnitude;

        if (distance > 0.0001f &&
            Physics.SphereCast(head, collisionRadius, toCamera / distance, out RaycastHit hit, distance, cameraCollisionLayer, QueryTriggerInteraction.Ignore))
            return false;

        return !Physics.CheckSphere(cameraPosition, cameraClearanceRadius, cameraCollisionLayer, QueryTriggerInteraction.Ignore);
    }

    // Finds how far back the camera can sit (zoom values are negative: more negative = farther)
    // before the line to the player is blocked or the camera would be touching something.
    float FindSafeZoom(Quaternion tiltRotation, Vector3 rootPosition, Vector3 head, float offsetY, float desiredZoom)
    {
        if (IsCameraSpotClear(head, CameraPositionAtZoom(tiltRotation, rootPosition, offsetY, desiredZoom)))
            return desiredZoom;

        float blocked = desiredZoom;
        float clear = zoomMin; // closest the camera is allowed to get
        for (int i = 0; i < 6; i++)
        {
            float mid = (blocked + clear) * 0.5f;
            if (IsCameraSpotClear(head, CameraPositionAtZoom(tiltRotation, rootPosition, offsetY, mid)))
                clear = mid;
            else
                blocked = mid;
        }

        return clear;
    }

    // 0 = camera can sit at its full distance, 1 = it is pushed all the way in
    float SqueezeAmount(float zoom, float desiredZoom)
    {
        return Mathf.InverseLerp(desiredZoom, zoomMin, zoom);
    }

    // Swings the classic camera sideways (by turning the tilt rig around the player) when the
    // spot straight behind the player is cramped and one side has clearly more room. It always
    // evaluates the camera's NATURAL position (without the swing), so it doesn't flip back and
    // forth: once the swing opens up room, the natural spot is still judged cramped and the swing stays.
    void UpdateWallSwing(Vector3 head, float offsetY, float desiredZoom)
    {
        if (pitchRoot == null) return;

        bool allowed = !FreeCameraEnabled && wallSwingAngle > 0.01f;
        if (allowed)
        {
            PlayerWeaponSystem weapon = Player.instance != null ? Player.instance.playerWeaponSystem : null;
            if (weapon != null && weapon.isAiming) allowed = false;
        }

        float targetYaw = 0f;

        if (allowed)
        {
            float tiltX = NormalizeAngle(pitchRoot.localEulerAngles.x);
            Quaternion natural = m_CameraTransform.rotation * Quaternion.Euler(tiltX, 0f, 0f);
            Vector3 root = pitchRoot.position;

            float naturalZoom = FindSafeZoom(natural, root, head, offsetY, desiredZoom);
            float naturalSqueeze = SqueezeAmount(naturalZoom, desiredZoom);

            // Hysteresis so it doesn't flicker on/off around the threshold
            if (!wallSwingActive && naturalSqueeze > wallSqueezeThreshold)
                wallSwingActive = true;
            else if (wallSwingActive && naturalSqueeze < wallSqueezeThreshold * 0.6f)
                wallSwingActive = false;

            if (wallSwingActive)
            {
                Quaternion leftRot = Quaternion.AngleAxis(-wallSwingAngle, Vector3.up) * natural;
                Quaternion rightRot = Quaternion.AngleAxis(wallSwingAngle, Vector3.up) * natural;
                float leftSqueeze = SqueezeAmount(FindSafeZoom(leftRot, root, head, offsetY, desiredZoom), desiredZoom);
                float rightSqueeze = SqueezeAmount(FindSafeZoom(rightRot, root, head, offsetY, desiredZoom), desiredZoom);

                float best = Mathf.Min(leftSqueeze, rightSqueeze);
                // Only worth swinging if it genuinely gains room
                if (best < naturalSqueeze - 0.2f)
                    targetYaw = leftSqueeze < rightSqueeze ? -wallSwingAngle : wallSwingAngle;
            }
        }
        else
        {
            wallSwingActive = false;
        }

        wallYaw = Mathf.SmoothDampAngle(wallYaw, targetYaw, ref wallYawVelocity, wallSwingSmoothTime);

        if (!FreeCameraEnabled && (Mathf.Abs(wallYaw) > 0.05f || pitchDirty))
        {
            float tiltNow = NormalizeAngle(pitchRoot.localEulerAngles.x);
            pitchRoot.localRotation = Quaternion.Euler(tiltNow, wallYaw, 0f);
            pitchDirty = true; // lets RestoreClassicPitch clear the swing again if we leave classic mode
        }
    }

    void CachePlayerRenderers()
    {
        playerRenderers.Clear();
        if (Player.instance == null) return;

        Transform playerTransform = Player.instance.transform;
        Transform aimRoot = playerTransform.Find("AimRoot"); // first-person arms/weapon live here and manage themselves

        foreach (Renderer r in playerTransform.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
            if (!r.enabled) continue;
            if (aimRoot != null && r.transform.IsChildOf(aimRoot)) continue;

            playerRenderers.Add(new CachedRenderer { renderer = r, shadowMode = r.shadowCastingMode });
        }
    }

    // Hides the player's body (shadow stays) while the camera is squeezed up against them, so the
    // camera never shows the inside of the model. Uses a distance gap between hide and show so it doesn't flicker.
    void UpdatePlayerVisibility(Vector3 head)
    {
        if (!hidePlayerWhenCameraClose)
        {
            SetPlayerHidden(false);
            return;
        }

        PlayerWeaponSystem weapon = Player.instance != null ? Player.instance.playerWeaponSystem : null;
        if (weapon != null && weapon.isAiming)
        {
            SetPlayerHidden(false);
            return;
        }

        float distance = Vector3.Distance(m_CameraPositionOffset.position, head);
        if (!playerHidden && distance < hidePlayerDistance)
            SetPlayerHidden(true);
        else if (playerHidden && distance > showPlayerDistance)
            SetPlayerHidden(false);
    }

    void SetPlayerHidden(bool hidden)
    {
        if (playerHidden == hidden) return;
        playerHidden = hidden;

        for (int i = 0; i < playerRenderers.Count; i++)
        {
            Renderer r = playerRenderers[i].renderer;
            if (r == null) continue;
            r.shadowCastingMode = hidden ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : playerRenderers[i].shadowMode;
        }
    }

    // The original single-line collision check, kept for when Wall Avoidance is turned off.
    void CheckCameraCollisionLegacy()
    {
        Vector3 pivotPosition = followTarget.position + new Vector3(0f, m_CameraPositionOffset.localPosition.y, 0f);

        // A character viewed head-on (arms, wider silhouette) fills the frame more than the
        // same distance viewed from behind, so pull the camera back extra as it swings toward
        // facing the player - gives proper breathing room during the front-facing reveal
        // instead of feeling cramped/in-your-face at the normal over-the-shoulder distance.
        float desiredZoom = zoomMax - (frontSwingExtraDistance * frontBlend);
        float distance = Mathf.Abs(desiredZoom);

        Vector3 backDirection = -m_CameraTransform.forward;

        // The free camera tilts up/down, so the camera sits along the tilted rig's back axis -
        // cast along that instead so walls/floor are detected where the camera really is.
        if (FreeCameraEnabled && pitchRoot != null)
        {
            pivotPosition = followTarget.position + pitchRoot.rotation * new Vector3(0f, m_CameraPositionOffset.localPosition.y, 0f);
            backDirection = -pitchRoot.forward;
        }

        Vector3 desiredWorldPos = pivotPosition + (backDirection * distance);
        Vector3 direction =  desiredWorldPos - pivotPosition;

        if (Physics.SphereCast(pivotPosition,collisionRadius,direction.normalized,out RaycastHit hit,distance,cameraCollisionLayer))
        {
            desiredZoom = -(hit.distance - collisionOffset);
            desiredZoom = Mathf.Clamp(desiredZoom, zoomMax, zoomMin);
        }

        // SmoothDamp removes jitter completely
        float smoothZoom = Mathf.SmoothDamp(m_CameraPositionOffset.localPosition.z, desiredZoom, ref zoomVelocity, zoomSmoothTime); // smooth time (tweak 0.05-0.15));

        Vector3 localPos = m_CameraPositionOffset.localPosition;
        localPos.z = smoothZoom;
        m_CameraPositionOffset.localPosition = localPos;
    }
}
