using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] InputActionReference movementAction;
    [SerializeField] InputActionReference sprintAction;
    [SerializeField] InputActionReference autoRotateAction;

    public float Speed = 5.0f;
    public float SprintSpeed = 10.0f;
    public float acceleration = 10.0f;

    public float sprintStaminaCost = 0.3f;
    public float rotationSpeed = 2f;
    public float strafeRotationSpeed = 5f;
    public float animationSmoothness = 0.3f;
    public float turningAnimationSmoothness = 0.2f;
    public bool useStaminaForSprint = true;
    public float pushForce = 3f; // Force applied to push rigidbodies

    private Vector2 moveInput;
    private float Gravity = -20.0f;


    public Vector2 GetMoveInput => moveInput;

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

    private void OnEnable()
    {
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
        HandleMovement();
        HandleAnimation();
        HandleAutoRotation();
    }

    void HandleAutoRotation()
    {
        //Check If EyePeak Mode is activated
        if (EyePeakHandler.instance && EyePeakHandler.instance.IsEyePeakActivated())
            return;

        if (autoRotateAction.action.WasPressedThisFrame() && moveInput.y < -0.1f)
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
            return;

        //Check if player is attacking or rolling
        if (player.isAttacking || player.isRolling || player.isScared || playerWeaponSystem.isReloading)
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

        float sprintInput = sprintAction.action.ReadValue<float>();

        // Calculate the forward vector
        Vector3 camForward_Dir = Vector3.Scale(cam.transform.forward, new Vector3(1, 0, 1)).normalized;
        //moveDirection = moveInput.y * camForward_Dir + moveInput.x * cam.transform.right;

        moveDirection = moveInput.y * transform.forward + moveInput.x * transform.right * 0.5f;

        //Check if player is grounded
        if (_characterController.isGrounded && moveInput.magnitude > 0.1f)
        {
            if (playerMovementLimiter != null && playerMovementLimiter.movementLimitActive)
            {
                targetSpeed = playerMovementLimiter.limitMoveSpeed;
            }
            else
            {
                //Only allow playe to sprint in forward direction
                if (sprintInput > 0.1f && moveInput.y > 0)
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

        //Check if player is Aiming then disable player rotation towards move direction
        if (playerWeaponSystem.isAiming == false)
        {
            //Check if player is Reloading
            if (playerWeaponSystem.isReloading == false)
            {
                //Movement
                if (Mathf.Abs(moveInput.y) > 0.1f)
                {
                    currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
                    Vector3 moveVector = moveDirection.normalized * currentSpeed * Time.deltaTime;
                    _characterController.Move(moveVector);
                }
            }

            //Look at movement direction
            if (moveInput.magnitude > 0.1f)
            {
                //Only rotate when moving forward or left and right
                if (moveInput.y > 0 || (Mathf.Abs(moveInput.y) < 0.1f && Mathf.Abs(moveInput.x) > 0.1f))
                {
                    Quaternion lookRotation = Quaternion.LookRotation(moveDirection);
                    Quaternion rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, lookRotation.eulerAngles.y, transform.rotation.eulerAngles.z);

                    if(Mathf.Abs(moveInput.y) < 0.1f && Mathf.Abs(moveInput.x) > 0.1f)
                    {
                        //Strafe
                        transform.rotation = Quaternion.Lerp(transform.rotation, rotation, strafeRotationSpeed * Time.deltaTime);
                    }
                    else
                    {
                        //Walking
                        transform.rotation = Quaternion.Lerp(transform.rotation, rotation, rotationSpeed * Time.deltaTime);
                    }
                }
            }
        }
    }

    float Normalize(float value, float min, float max)
    {
        return (value - min) / (max - min);
    }

    //Handle Player Animations Behaviour
    private void HandleAnimation()
    {
        float currentX = player.animator.GetFloat("x");
        float currentY = player.animator.GetFloat("y");
        float currentVelocity = player.animator.GetFloat("Velocity");

        float speed = animationSmoothness; // e.g. 5�10 for fast, 1�3 for slow

        //Apply Animation
        if (player.IsDead() == false)
        {
            player.animator.SetFloat("Velocity", Mathf.MoveTowards(currentVelocity, _characterController.velocity.magnitude, speed * Time.deltaTime));
        }
        else
        {
            player.animator.SetFloat("Velocity", Mathf.MoveTowards(currentVelocity, 0, speed * Time.deltaTime));
        }

        //Set Animation
        if (Mathf.Abs(moveInput.y) < 0.1f && Mathf.Abs(moveInput.x) > 0.1f)
        {
            //Strafe
            player.animator.SetFloat("x", Mathf.MoveTowards(currentX, 0, speed * Time.deltaTime));
            player.animator.SetFloat("y", Mathf.MoveTowards(currentY, 0, speed * Time.deltaTime));

            player.animator.SetInteger("TurningDirection", moveInput.x > 0.1 ? 0 : 1);
            player.animator.SetBool("Turning", true);
        }
        else
        {
            player.animator.SetBool("Turning", false);

            //Not Strafe
            player.animator.SetFloat("x", Mathf.MoveTowards(currentX, moveInput.x, turningAnimationSmoothness * Time.deltaTime));

            if (playerWeaponSystem.isReloading == false)
                player.animator.SetFloat("y", Mathf.MoveTowards(currentY, moveInput.y, turningAnimationSmoothness * Time.deltaTime));
            else
                player.animator.SetFloat("y", Mathf.MoveTowards(currentY, 0, speed * Time.deltaTime));
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
