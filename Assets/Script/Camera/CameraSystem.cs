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
    [SerializeField] float rotationEngageAngle = 1f;
    [Tooltip("If the CHARACTER's own facing snaps by more than this in a single frame (the dedicated quick-180 action), the camera cuts instantly instead of swinging through it. Ordinary sustained turning never triggers this, no matter how far the camera ends up lagging behind.")]
    [SerializeField] float instantCutAngle = 150f;
    [Tooltip("Roughly how long (seconds) the camera takes to ease into catching up - gives it real weight/momentum instead of a mechanical constant-speed correction. Larger = more visible independent swing.")]
    [SerializeField] float tankCameraRotationSmoothTime = 0.4f;
    [Tooltip("Hard cap on turn speed while catching up - must stay above the character's own turn speed (150 deg/sec) or the camera will never fully close the gap during a sustained turn.")]
    [SerializeField] float tankCameraMaxDegreesPerSecond = 170f;
    [Tooltip("How long (seconds) the camera's position takes to catch up to the player - gives it independent inertia instead of being rigidly glued to the player's position.")]
    [SerializeField] float tankCameraPositionSmoothTime = 0.55f;
    [SerializeField] bool showDebugOverlay = false;
    [Tooltip("Key that snaps the camera back behind the player on demand.")]
    [SerializeField] Key recenterKey = Key.F8;

    [Header("Continuous-Turn Front Swing")]
    [Tooltip("How many FULL 360-degree turns the player must make in the same direction, without letting go of the turn key, before the camera swings around to face the player from the front. Ordinary turning (corners, a U-turn, even a couple of spins) keeps the camera behind the player.")]
    [SerializeField] float turnsToFront = 4f;
    [Tooltip("How long (seconds) the front/behind blend takes to ease in and out - gives it a natural deceleration instead of stopping dead.")]
    [SerializeField] float frontBlendSmoothTime = 0.5f;
    [Tooltip("Minimum turn input required to count as 'sustaining a turn' (ignores tiny stick drift).")]
    [SerializeField] float sustainedTurnInputThreshold = 0.3f;
    [Tooltip("Extra distance the camera pulls back (added on top of the normal distance) as it swings toward facing the player head-on, so the front-facing reveal has proper breathing room instead of feeling cramped.")]
    [SerializeField] float frontSwingExtraDistance = 2.2f;

    [Header("Spawn / Teleport")]
    [Tooltip("If the player ends up more than this many metres from where they were last frame (spawn point, checkpoint load, save load, teleport), the camera cuts straight to behind them instead of gliding over from the old spot. It also always cuts behind the player on the very first frame.")]
    [SerializeField] float teleportSnapDistance = 4f;

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
    Vector3 lastFollowPosition;
    bool hasFollowPosition;
    bool aimWasActive;            // aiming state last frame, used to re-seat the camera when aiming ends
    bool pitchDirty;
    float recenterYawVelocity;
    float recenterPitchVelocity;
    float pitchRestoreVelocity;

    float camYawVelocity;
    Vector3 camPositionVelocity;
    Quaternion lastPlayerRotation;
    bool hasLastPlayerRotation;
    float sustainedTurnDirection;
    float sustainedTurnAngle;     // degrees the player has turned in sustainedTurnDirection without a break
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
    [SerializeField] float hidePlayerDistance = 0.68f;
    [Tooltip("The body reappears once the camera is farther than this. Keep it a bit larger than the hide distance so it doesn't flicker.")]
    [SerializeField] float showPlayerDistance = 0.85f;

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
    Vector3 fixedCamPos, fixedCamVelocity, fixedLookPoint, fixedLookVelocity;
    Quaternion fixedCamRot = Quaternion.identity;
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

        // First frame, or the player was moved a long way in one go (spawn point, checkpoint/save
        // load, teleport): cut straight to behind them rather than gliding over from the old spot.
        if (followTarget != null && lookAtTarget != null)
        {
            bool teleported = !hasFollowPosition ||
                (followTarget.position - lastFollowPosition).sqrMagnitude > teleportSnapDistance * teleportSnapDistance;
            if (teleported && activeZone == null)
                SnapBehindPlayer();
            lastFollowPosition = followTarget.position;
            hasFollowPosition = true;
        }

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
            PlayerWeaponSystem aimWeapon = Player.instance != null ? Player.instance.playerWeaponSystem : null;
            bool aimingNow = aimWeapon != null && aimWeapon.isAiming;
            if (aimWasActive && !aimingNow && !FreeCameraEnabled)
                SnapBehindPlayer();
            aimWasActive = aimingNow;

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

    // Cuts the rig straight to behind the player (first frame, teleports, end of aiming).
    // While aiming, the first-person aim camera is live and the player can spin to any direction, so
    // this rig keeps its old position/heading. When aiming ends, put it straight back behind the
    // player (the Cinemachine blend then eases the view there) instead of waiting for the player
    // to move and the camera to swing round from wherever it was left.
    void SnapBehindPlayer()
    {
        m_CameraTransform.rotation = Quaternion.LookRotation(lookAtTarget.forward, Vector3.up);
        m_CameraTransform.position = followTarget.position;
        camYawVelocity = 0f;
        camPositionVelocity = Vector3.zero;
        frontBlend = 0f;
        frontBlendVelocity = 0f;
        sustainedTurnDirection = 0f;
        sustainedTurnAngle = 0f;
        wallYaw = 0f;
        wallYawVelocity = 0f;
        wallSwingActive = false;
        zoomVelocity = 0f;
        freeSynced = false;
        recentering = false;
        lastPlayerRotation = m_PlayerMovement.transform.rotation;
        hasLastPlayerRotation = true;
        debugLastEvent = "SNAP BEHIND";
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
        sustainedTurnAngle = 0f;
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

        RestoreClassicPitch();
        Vector3 playerPos = m_PlayerMovement.transform.position;
        fixedLookPoint = playerPos + Vector3.up * zone.lookHeight;
        fixedLookVelocity = Vector3.zero;
        if (zone.cutOnEnter)
        {
            fixedCamPos = zone.GetCameraPosition(playerPos);
            fixedCamRot = zone.trackPlayer ? zone.GetTrackingRotation(fixedCamPos, fixedLookPoint) : zone.GetCameraRotation();
        }
        else
        {
            // blend in from wherever the camera is right now
            fixedCamPos = m_CameraPositionOffset.position;
            fixedCamRot = m_CameraPositionOffset.rotation;
        }
        fixedCamVelocity = Vector3.zero;
        PlaceRigForCamera(fixedCamPos, fixedCamRot);
    }

    // snapBehind: cut straight back behind the player (used when leaving a zone by teleport, e.g. walking out of an interior)
    public void ExitFixedZone(CameraFixedZone zone, bool snapBehind = false)
    {
        if (activeZone != zone) return;
        activeZone = null;
        // (targets can already be gone when this comes from OnDisable during scene unload / leaving Play mode)
        if (snapBehind && lookAtTarget != null && followTarget != null && m_CameraTransform != null && m_PlayerMovement != null)
            SnapBehindPlayer();
    }

    // The camera's own pose is what gets smoothed (not the rig root's), so moving/rotating the anchor turns the camera in place
    // instead of swinging it around the rig's pivot 2.5 m away. blendTime 0 = locked exactly on the anchor.
    // Tracking zones: the camera stays around the anchor (drifting a little with the player, kept in the zone's bounds) and
    // turns to keep the player in view; the aim point is smoothed so the view follows lazily instead of jittering.
    void HandleFixedZoneCamera()
    {
        CameraFixedZone z = activeZone;
        Vector3 playerPos = m_PlayerMovement.transform.position;
        Vector3 targetPos = z.GetCameraPosition(playerPos);
        float drift = z.blendTime > 0f ? z.blendTime : 0.5f;           // drift always glides; blendTime 0 only locks a static anchor
        if (z.followAmount <= 0f && z.blendTime <= 0f) { fixedCamPos = targetPos; fixedCamVelocity = Vector3.zero; }
        else fixedCamPos = Vector3.SmoothDamp(fixedCamPos, targetPos, ref fixedCamVelocity, drift);

        Quaternion targetRot;
        if (z.trackPlayer)
        {
            Vector3 look = playerPos + Vector3.up * z.lookHeight;
            fixedLookPoint = z.trackSmoothTime <= 0f ? look : Vector3.SmoothDamp(fixedLookPoint, look, ref fixedLookVelocity, z.trackSmoothTime);
            targetRot = z.GetTrackingRotation(fixedCamPos, fixedLookPoint);
            fixedCamRot = targetRot;
        }
        else
        {
            targetRot = z.GetCameraRotation();
            fixedCamRot = z.blendTime <= 0f ? targetRot : Quaternion.Slerp(fixedCamRot, targetRot, 1f - Mathf.Exp(-3f * Time.deltaTime / z.blendTime));
        }
        PlaceRigForCamera(fixedCamPos, fixedCamRot);
    }

    // The virtual camera sits under the rig root (pitch + offset), so putting the ROOT on the anchor would leave the actual
    // view 2.5 m behind and pitched down from it. Undo that chain so the camera itself ends up exactly at camPos/camRot.
    void PlaceRigForCamera(Vector3 camPos, Quaternion camRot)
    {
        Quaternion localRot = Quaternion.Inverse(m_CameraTransform.rotation) * m_CameraPositionOffset.rotation;
        Vector3 localPos = Quaternion.Inverse(m_CameraTransform.rotation) * (m_CameraPositionOffset.position - m_CameraTransform.position);
        Quaternion rigRot = camRot * Quaternion.Inverse(localRot);
        m_CameraTransform.SetPositionAndRotation(camPos - rigRot * localPos, rigRot);
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
            sustainedTurnAngle = 0f;
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

    // Tracks how far (in degrees) the player has turned continuously in one direction, and only
    // once that reaches turnsToFront full rotations does frontBlend ease from 0 (camera behind the
    // player) to 1 (camera swung around, facing back at the player). Releasing the turn key or
    // reversing direction restarts the count and eases the camera back behind. Freezes while the
    // player is fully idle, consistent with the rest of the camera settling in place.
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
                sustainedTurnAngle = 0f;
            }
            else if (hasLastPlayerRotation)
            {
                float yawDelta = Mathf.DeltaAngle(lastPlayerRotation.eulerAngles.y, m_PlayerMovement.transform.eulerAngles.y);
                if (Mathf.Abs(yawDelta) < instantCutAngle) // ignore the quick-180 flip
                    sustainedTurnAngle += Mathf.Abs(yawDelta);
            }
        }
        else
        {
            sustainedTurnDirection = 0f;
            sustainedTurnAngle = 0f;
        }

        float targetBlend = sustainedTurnAngle >= turnsToFront * 360f ? 1f : 0f;

        // SmoothDamp instead of MoveTowards - MoveTowards moves at a flat constant rate and
        // then halts dead the instant it reaches the target, with zero deceleration.
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
        GUI.Label(new Rect(10, 140, 500, 20), "Front swing blend: " + frontBlend.ToString("F2") + " (0=behind, 1=facing player from front) turned=" + sustainedTurnAngle.ToString("F0") + "/" + (turnsToFront * 360f).ToString("F0") + " deg");
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
