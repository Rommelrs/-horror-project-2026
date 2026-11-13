using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] InputActionReference movementAction;
    public InputAction MoveAction => movementAction != null ? movementAction.action : null;
    [SerializeField] InputActionReference sprintAction;
    [SerializeField] InputActionReference autoRotateAction;

    public float Speed = 5.0f;
    public float SprintSpeed = 10.0f;
    [Tooltip("Walking speed while reloading (from the hip).")]
    public float ReloadMoveSpeed = 0.9f;
    public float acceleration = 10.0f;

    public float sprintStaminaCost = 0.3f;
    public float rotationSpeed = 2f;
    public float animationSmoothness = 0.3f;
    public float turningAnimationSmoothness = 0.2f;
    public bool useStaminaForSprint = true;
    public float pushForce = 3f; // Force applied to push rigidbodies

    [Header("Tank Controls")]
    [Tooltip("Degrees per second the character rotates in place under A/D input.")]
    [SerializeField] float tankTurnSpeed = 150f;
    [Tooltip("Backward movement speed multiplier (classic tank controls can't sprint backward and walk slower in reverse).")]
    [SerializeField] [Range(0.1f, 1f)] float tankBackwardSpeedMultiplier = 0.5f;

    [Header("Turning In Place (standing still)")]
    [Tooltip("How quickly he gets up to full turning speed while A/D is held (degrees per second, per second). A tap turns him a little, holding turns him smoothly at Tank Turn Speed.")]
    [SerializeField] float turnAcceleration = 600f;
    [Tooltip("How quickly he stops turning once A/D is let go (degrees per second, per second).")]
    [SerializeField] float turnDeceleration = 900f;
    [Tooltip("Degrees turned LEFT for one full cycle of the turn animation (Khalid_TurnLeft90: 90). The animation plays only as far as he really turns, so a tap shows part of it.")]
    [SerializeField] float turnDegreesPerCycle = 90f;
    [Tooltip("Same for turning RIGHT (Khalid_TurnRight90's mocap turns 102.6 deg).")]
    [SerializeField] float turnDegreesPerCycleRight = 102.6f;
    [Tooltip("Where in the turn clip a foot lands (0-1): a footstep (surface sound, like walking) plays as the turn passes it. Played from here, not as clip events: a clip whose time follows a parameter fires its events twice when it wraps round.")]
    [SerializeField] float[] turnStepsLeft = { 0.38f, 0.85f };
    [SerializeField] float[] turnStepsRight = { 0.27f, 0.90f };
    PlayerEventHandler turnStepEvents;

    void TurnFootsteps(float from, float to, float[] steps)
    {
        if (steps == null || PlayerMeleeWeapon.Ready) return;      // pipe raised: the turn clip isn't playing
        if (turnStepEvents == null) turnStepEvents = GetComponentInChildren<PlayerEventHandler>();
        if (turnStepEvents == null) return;
        foreach (float s in steps)
        {
            bool passed = to >= from ? (s > from && s <= to) : (s > from || s <= to);   // the second case wraps round 1 -> 0
            if (passed) turnStepEvents.Footstep();
        }
    }

    private Vector2 moveInput;
    private float Gravity = -20.0f;


    public Vector2 GetMoveInput => moveInput;

    // Lets a scripted sequence (e.g. AutoPickupCutscene) drive the movement animation blend
    // directly while player.pauseMovement is suppressing normal input processing.
    public void SetExternalMoveInput(Vector2 value)
    {
        moveInput = value;
    }
    // Current smoothed movement speed (0 = standing still, up to SprintSpeed) - lets other
    // systems (like the camera's front-swing timing) react to how fast the player is moving.
    public float CurrentSpeed => currentSpeed;

    CharacterController _characterController;
    PlayerWeaponSystem playerWeaponSystem;
    Camera cam;
    Player player;
    PlayerStamina playerStamina;
    Health health;
    PlayerMovementLimiter playerMovementLimiter;

    float targetSpeed;
    float currentSpeed;
    Vector3 moveDirection;

    // Used to measure how fast the player is really moving (see GetMeasuredHorizontalSpeed)
    Vector3 lastAnimPosition;
    bool hasLastAnimPosition;

    // turning in place: current turn speed (deg/s, + = right) and how far through the turn animation's cycle he is
    float turnVelocity;
    float turnPhase;
    bool turningInPlace;
    bool tankMovedThisFrame;
    static readonly int TurnPhaseParam = Animator.StringToHash("TurnPhase");
    bool hasTurnPhaseParam, checkedTurnPhaseParam;

    private void OnEnable()
    {
        hasLastAnimPosition = false;

        //Enable the input actions
        movementAction.action.Enable();
        sprintAction.action.Enable();
        autoRotateAction.action.Enable();
    }

    private void OnDisable()
    {
        //Disable the input actions
        movementAction.action.Disable();
        sprintAction.action.Disable();
        autoRotateAction.action.Disable();
    }

    private void OnDestroy()
    {
        //Unsubscribe from health events
        if (health != null)
            health.OnDamageTaken -= GetHit;
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        playerWeaponSystem = GetComponent<PlayerWeaponSystem>();
        playerMovementLimiter = GetComponent<PlayerMovementLimiter>();
        player = GetComponent<Player>();
        playerStamina = GetComponent<PlayerStamina>();
        health = GetComponent<Health>();
    }

    private void Start()
    {
        cam = Camera.main;

        //Subscribe to health events
        if (health != null)
            health.OnDamageTaken += GetHit;
    }

    private void Update()
    {
        HandleGravity();
        tankMovedThisFrame = false;
        HandleMovement();
        // anything that stopped normal movement this frame (aiming, a cutscene, an attack, a grab...) drops the turn
        if (!tankMovedThisFrame) { turnVelocity = 0f; turningInPlace = false; }
        HandleAnimation();
        HandleAutoRotation();
    }

    void HandleAutoRotation()
    {
        //Check If EyePeak Mode is activated
        if (EyePeakHandler.instance && EyePeakHandler.instance.IsEyePeakActivated())
            return;

        if (autoRotateAction.action.WasPressedThisFrame() && moveInput.y < -0.1f && !PlayerMeleeWeapon.Ready)
        {
            if (!playerWeaponSystem.isAiming)
            {
                //Third Person Mode
                Vector3 angle = transform.eulerAngles;
                angle.y += 180;
                transform.rotation = Quaternion.Euler(angle);
            }
            else
            {
                //First Person Mode
                playerWeaponSystem.AutoRotate();
            }
        }
    }

    //Handle Player Hit
    void GetHit(int damage)
    {
        //Trigger Knockback animation
        player.animator.SetTrigger("Knockback");
    }

    //Handle Player Gravity
    private void HandleGravity()
    {
        //Check if player is grounded
        if (_characterController.isGrounded == false)
        {
            //Apply gravity
            _characterController.Move(new Vector3(0f, Gravity, 0f) * Time.deltaTime);
        }
    }

    //Handle Player Movement
    private void HandleMovement()
    {
        //Check if movement is paused
        if (player.pauseMovement)
        {
            // Without this, moveInput keeps whatever value it had the instant movement got
            // paused (e.g. still holding W), so the animator keeps blending toward a walk/run
            // pose forever even though the character is frozen in place.
            moveInput = Vector2.zero;
            return;
        }

        //Check if player is attacking or rolling (reloading is allowed - he walks slowly, see below)
        if (player.isAttacking || player.isRolling || player.isScared || player.isGrabbed)
            return;

        //Check if player is dead
        if (player.IsDead())
            return;

        //Check If EyePeak Mode is activated
        if (EyePeakHandler.instance && EyePeakHandler.instance.IsEyePeakActivated())
            return;

        //Check if game is over ow won
        if (LevelManager.instance != null && (LevelManager.instance.isGameOver || LevelManager.instance.isGameWon))
            return;

        //Movement Input
        moveInput = movementAction.action.ReadValue<Vector2>();

        //Melee pipe raised: PlayerMeleeWeapon walks / strafes / turns him
        if (PlayerMeleeWeapon.Ready)
        {
            currentSpeed = 0f;
            return;
        }

        float sprintInput = sprintAction.action.ReadValue<float>();

        // Only forward/back input translates - left/right rotates the character in place instead.
        bool hasMoveInput = Mathf.Abs(moveInput.y) > 0.1f;

        //Check if player is grounded
        if (_characterController.isGrounded && hasMoveInput)
        {
            if (playerMovementLimiter != null && playerMovementLimiter.movementLimitActive)
            {
                targetSpeed = playerMovementLimiter.limitMoveSpeed;
            }
            else
            {
                //Only allow playe to sprint in forward direction (never while using a healing item - he walks through the animation)
                if (sprintInput > 0.1f && moveInput.y > 0 && !PlayerQuickHeal.Using)
                {
                    if (playerStamina != null)
                    {
                        //Stamina System is active

                        //Use Stamina from Player Stamina System
                        if (useStaminaForSprint && playerStamina.Stamina >= sprintStaminaCost * Time.deltaTime && !playerWeaponSystem.isAiming)
                        {
                            playerStamina.UseStamina(sprintStaminaCost * Time.deltaTime);
                            targetSpeed = SprintSpeed;
                        }
                        else
                        {
                            targetSpeed = Speed;
                        }
                    }
                    else
                    {
                        //Stamina System is disabled
                        targetSpeed = SprintSpeed;
                    }

                    //Disable sprinting if Aiming
                    if (playerWeaponSystem.isAiming)
                        targetSpeed = Speed;

                    //Disable sprinting if reloading
                    if (playerWeaponSystem.isReloading)
                        targetSpeed = Speed;
                }
                else
                {
                    targetSpeed = Speed;
                }
            }
        }
        else
            targetSpeed = 0.0f;

        //Check if player is Aiming then disable locomotion control (an aimed reload is first person and stays put)
        if (playerWeaponSystem.isAiming)
            return;

        //Reloading from the hip: he can still walk, but slowly (no sprint)
        if (playerWeaponSystem.isReloading)
            targetSpeed = Mathf.Min(targetSpeed, ReloadMoveSpeed);

        //The Questioner's scream slows him for a moment
        targetSpeed *= PlayerCloseCombat.SpeedMultiplier;

        HandleTankMovement();
    }

    // Tank controls: A/D rotate the character in place at a fixed turn speed (independent of
    // movement), forward/back always translate along the character's own current facing.
    // Backward movement is slower and can never sprint, matching classic tank-control feel.
    private void HandleTankMovement()
    {
        tankMovedThisFrame = true;
        bool standing = Mathf.Abs(moveInput.y) <= 0.1f;
        bool turnInput = Mathf.Abs(moveInput.x) > 0.1f;
        float target = turnInput ? moveInput.x * tankTurnSpeed : 0f;
        if (standing)
        {
            // like a person: he builds up into the turn and settles out of it, so a tap is a small turn and holding is a smooth one
            bool speedingUp = turnInput && Mathf.Sign(target) == Mathf.Sign(turnVelocity) && Mathf.Abs(target) > Mathf.Abs(turnVelocity);
            bool fromRest = turnInput && Mathf.Abs(turnVelocity) < 0.01f;
            float rate = speedingUp || fromRest ? turnAcceleration : turnDeceleration;
            turnVelocity = Mathf.MoveTowards(turnVelocity, target, rate * Time.deltaTime);
        }
        else
            turnVelocity = target;     // walking: steering answers at once, as before
        float turned = turnVelocity * Time.deltaTime;
        if (Mathf.Abs(turned) > 0f) transform.Rotate(0f, turned, 0f);
        turningInPlace = standing && Mathf.Abs(turnVelocity) > 1f;
        // each new turn starts the step from its beginning; the clip advances by the degrees really turned (its mocap turned
        // turnDegreesPerCycle left / turnDegreesPerCycleRight right, with the body's own turn taken out - the transform does that)
        if (turningInPlace)
        {
            float before = turnPhase;
            turnPhase = Mathf.Repeat(turnPhase + Mathf.Abs(turned) / Mathf.Max(1f, turned > 0f ? turnDegreesPerCycleRight : turnDegreesPerCycle), 1f);
            TurnFootsteps(before, turnPhase, turned > 0f ? turnStepsRight : turnStepsLeft);
        }
        else turnPhase = 0f;

        moveDirection = transform.forward;

        if (Mathf.Abs(moveInput.y) > 0.1f)
        {
            float directionMultiplier = moveInput.y > 0f ? 1f : -tankBackwardSpeedMultiplier;
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
            Vector3 moveVector = moveDirection * directionMultiplier * currentSpeed * Time.deltaTime;
            _characterController.Move(moveVector);
        }
        else
        {
            currentSpeed = Mathf.Lerp(currentSpeed, 0f, acceleration * Time.deltaTime);
        }
    }

    float Normalize(float value, float min, float max)
    {
        return (value - min) / (max - min);
    }

    // Horizontal speed measured from the player's real displacement this frame.
    // CharacterController.velocity only refreshes when Move() is called, so after a teleport or a
    // cutscene it can stay stuck at an old large value while the player stands still. The animator
    // then kept the run blend for a second or two as soon as the player started walking.
    private float GetMeasuredHorizontalSpeed()
    {
        Vector3 position = transform.position;
        float dt = Time.deltaTime;
        float speed = 0f;

        if (hasLastAnimPosition && dt > 0.0001f)
        {
            Vector3 delta = position - lastAnimPosition;
            delta.y = 0f;

            // A big jump in a single frame is a teleport (spawn point, cutscene), not walking
            if (delta.magnitude < 1f)
                speed = delta.magnitude / dt;
        }

        lastAnimPosition = position;
        hasLastAnimPosition = true;
        return speed;
    }

    //Handle Player Animations Behaviour
    private void HandleAnimation()
    {
        float measuredSpeed = GetMeasuredHorizontalSpeed();

        float currentX = player.animator.GetFloat("x");
        float currentY = player.animator.GetFloat("y");
        float currentVelocity = player.animator.GetFloat("Velocity");

        float speed = animationSmoothness; // e.g. 5�10 for fast, 1�3 for slow

        //Apply Animation
        if (player.IsDead() == false)
        {
            player.animator.SetFloat("Velocity", Mathf.MoveTowards(currentVelocity, measuredSpeed, speed * Time.deltaTime));
        }
        else
        {
            player.animator.SetFloat("Velocity", Mathf.MoveTowards(currentVelocity, 0, speed * Time.deltaTime));
        }

        // No strafing - pure left/right input rotates the character in place, which uses the
        // "turn in place" animation instead of a strafe blend.
        // (melee pipe raised: A/D strafe on the melee layer, not a turn)
        bool pureTurnInPlace = turningInPlace && !PlayerMeleeWeapon.Ready;

        //Set Animation
        if (pureTurnInPlace)
        {
            //Turning in place
            player.animator.SetFloat("x", Mathf.MoveTowards(currentX, 0, speed * Time.deltaTime));
            player.animator.SetFloat("y", Mathf.MoveTowards(currentY, 0, speed * Time.deltaTime));

            // the turn animation plays as far as he has really turned (its states use TurnPhase as their time)
            if (!checkedTurnPhaseParam)
            {
                checkedTurnPhaseParam = true;
                foreach (var p in player.animator.parameters) if (p.nameHash == TurnPhaseParam) hasTurnPhaseParam = true;
            }
            if (hasTurnPhaseParam) player.animator.SetFloat(TurnPhaseParam, turnPhase);

            player.animator.SetInteger("TurningDirection", turnVelocity > 0f ? 0 : 1);
            player.animator.SetBool("Turning", true);
        }
        else
        {
            player.animator.SetBool("Turning", false);

            // x-axis input is steering, not lateral movement, so it never drives the strafe blend.
            player.animator.SetFloat("x", Mathf.MoveTowards(currentX, 0f, turningAnimationSmoothness * Time.deltaTime));

            // reloading walks too (slowly), so the legs keep the walk blend
            player.animator.SetFloat("y", Mathf.MoveTowards(currentY, moveInput.y, turningAnimationSmoothness * Time.deltaTime));
        }
    }

    // Handle pushing rigidbodies when CharacterController collides with them
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        // Check if the object has a rigidbody and is not kinematic
        if (body == null || body.isKinematic)
            return;

        // Only push objects with the Box component (or specific tags)
        Box box = hit.collider.GetComponent<Box>();
        if (box == null)
            return;

        // Don't push objects below the player
        if (hit.moveDirection.y < -0.3f)
            return;

        // Only push when player is actively moving
        if (moveInput.magnitude < 0.1f)
            return;

        // Calculate push direction from player's movement direction
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

        // Apply force to the rigidbody
        body.linearVelocity = pushDir * pushForce;
    }
}
