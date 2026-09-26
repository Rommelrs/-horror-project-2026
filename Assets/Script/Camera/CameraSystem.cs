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

    Vector3 targetFollowPosition;
    Vector2 moveInput;
    bool wantsRecenter;

    CameraFixedZone activeZone;
    float lastZoneSwitchTime = -999f;
    const float zoneSwitchCooldown = 0.3f;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
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
    }

    private void LateUpdate()
    {
        var keyboard = Keyboard.current;
        wantsRecenter = keyboard != null && keyboard[recenterKey].wasPressedThisFrame;
        moveInput = m_PlayerMovement.GetMoveInput;

        if (activeZone != null)
        {
            HandleFixedZoneCamera();
        }
        else
        {
            HandlePosition();
            HandleRotation();
            CheckCameraCollision();
        }
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

    void CheckCameraCollision()
    {
        Vector3 pivotPosition = followTarget.position + new Vector3(0f, m_CameraPositionOffset.localPosition.y, 0f);

        // A character viewed head-on (arms, wider silhouette) fills the frame more than the
        // same distance viewed from behind, so pull the camera back extra as it swings toward
        // facing the player - gives proper breathing room during the front-facing reveal
        // instead of feeling cramped/in-your-face at the normal over-the-shoulder distance.
        float desiredZoom = zoomMax - (frontSwingExtraDistance * frontBlend);
        float distance = Mathf.Abs(desiredZoom);

        Vector3 desiredWorldPos = pivotPosition + (-m_CameraTransform.forward * distance);
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
