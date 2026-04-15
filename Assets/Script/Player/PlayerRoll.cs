using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerRoll : MonoBehaviour
{
    [SerializeField] InputActionReference rollInputAction;
    [SerializeField] float rollDuration = 1f; // Duration of the roll animation
    [SerializeField] float rollCooldownPeriod = 1f;
    [SerializeField] float rollForce = 1f;
    [SerializeField] float staminaCost = 10f;

    public bool IsRolling => isRolling;
    bool isRolling = false;
    Animator animator;
    CharacterController characterController;
    Player player;
    PlayerStamina playerStamina;
    PlayerWeaponSystem playerWeaponSystem;

    float readyToRollTime = 0;

    public delegate void RollStarted();
    public event RollStarted OnRollStarted;

    private void OnEnable()
    {
        //Enable the input action
        rollInputAction.action.Enable();
    }

    private void OnDisable()
    {
        //Disable the input action
        rollInputAction.action.Disable();
    }

    private void OnDestroy()
    {
        // Unsubscribe from the input action
        rollInputAction.action.performed -= OnRollInput;
    }

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        characterController = GetComponent<CharacterController>();
        player = GetComponent<Player>();
        playerStamina = GetComponent<PlayerStamina>();
        playerWeaponSystem = GetComponent<PlayerWeaponSystem>();
    }

    private void Start()
    {
        // Subscribe to the input action
        rollInputAction.action.performed += OnRollInput;

        readyToRollTime = Time.time;
    }

    //Handle Roll Behaviour
    void RollInputPressed()
    {
        //Check if it's already game over or won
        if (LevelManager.instance != null && (LevelManager.instance.isGameOver || LevelManager.instance.isGameWon))
            return;

        //Check If EyePeak Mode is activated
        if (EyePeakHandler.instance && EyePeakHandler.instance.IsEyePeakActivated())
            return;

        if (Player.instance.isScared)
            return;

        //DO not alllow rolling if player is aiming or reloading
        if (playerWeaponSystem != null && (playerWeaponSystem.isAiming || playerWeaponSystem.isReloading))
            return;

        //Check if player has enough stamina
        if (isRolling == false && Time.time > readyToRollTime &&  (playerStamina == null || playerStamina.Stamina >= staminaCost))
        {
            // Perform the roll action
            isRolling = true;

            //Set next roll ready time
            readyToRollTime = Time.time + rollDuration + rollCooldownPeriod;

            player.isRolling = true;
            animator.SetTrigger("Rolling");
            OnRollStarted?.Invoke();
            
            if(playerStamina != null)
            playerStamina.UseStamina(staminaCost);
            StartCoroutine(Co_ResetRollingAfterTime());
        }
    }

    //On Roll Input check if input is pressed
    private void OnRollInput(InputAction.CallbackContext context)
    {
        if (context.performed)
            RollInputPressed();
    }

    //Reset rolling state after a certain duration
    IEnumerator Co_ResetRollingAfterTime()
    {
        yield return new WaitForSeconds(rollDuration);
        isRolling = false;
        player.isRolling = false;
    }

    private void Update()
    {
        //Check if player is dead
        if (player.IsDead())
            return;

        //Check if it's already game over or won
        if (LevelManager.instance != null && (LevelManager.instance.isGameOver || LevelManager.instance.isGameWon))
            return;

        //Check if player is rolling
        if (isRolling)
        {
            // Apply roll force in the forward direction
            Vector3 moveDirection = transform.forward.normalized * rollForce;
            characterController.Move(moveDirection * Time.deltaTime);
        }
    }
}
