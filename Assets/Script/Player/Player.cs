using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.AI;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    public static Player instance;

    public GameObject playerModel;
    public Health health;
    public PlayerMovement playerMovement;
    public PlayerWeaponSystem playerWeaponSystem;
    public Animator animator;
    public Inventory inventory;
    public CharacterController controller;
    public PlayerStability playerStability;
    public BloodEffectHandler bloodEffectHandler;
    public PlayerMovementLimiter playerMovementLimiter;

    [SerializeField] Image damageFillImage;
    [SerializeField] float damageEffectAlpha = 0.5f;
    [SerializeField] float damageEffectDuration = 0.3f;

    public bool pauseMovement = false;
    public bool isAttacking = false;
    public bool isRolling = false;
    public bool isScared = false;
    public bool fuseBoxInRange = false;
    public bool hasMap = false;

    [HideInInspector] public Fusebox currentFuseboxInRange;

    private Coroutine damageEffectCoroutine;
    public UnityEvent OnPlayerDeath;
    public UnityEvent OnPlayerTelported;

    private void Awake()
    {
        instance = this;
        
        // Load hasMap from PlayerPrefs
        hasMap = PlayerPrefs.GetInt("HasMap", 0) == 1;
    }

    private void OnDestroy()
    {
        // Unsubscribe from health events
        if (health != null)
        {
            health.OnDeath.RemoveListener(PlayerDeath);
            health.OnDamageTaken -= TakeDamage;
        }
    }

    private void Start()
    {
        //Subscribe to health events
        if (health != null)
        {
            health.OnDeath.AddListener(PlayerDeath);
            health.OnDamageTaken += TakeDamage;
        }
        
        // Check if this is a new game and reset player state
        if (PlayerPrefs.GetInt("IsNewGame", 0) == 1)
        {
            Debug.Log("[Player] New game detected - resetting player state");
            ResetForNewGame();
            PlayerPrefs.DeleteKey("IsNewGame");
            PlayerPrefs.Save();
        }
    }

    float stabilityDecreaseCooldownPeriod = 40f;
    float nextStabilityDecreaseTime = 0f;

    //On Player Take Damage
    void TakeDamage(int damage)
    {
        // Handle damage logic
        if (damageEffectCoroutine != null) StopCoroutine(damageEffectCoroutine);
        damageEffectCoroutine = StartCoroutine(DamageEffectCoroutine());

        //Decrease player stability
        playerStability.DecreaseStability(15);

        //Check if bleeding
        if(bloodEffectHandler != null)
        {
            if (Time.time > nextStabilityDecreaseTime)
            {
                nextStabilityDecreaseTime = Time.time + stabilityDecreaseCooldownPeriod;

                //Decrease Stability
                playerStability.DecreaseStability(20);
            }
        }
    }

    // Coroutine to handle damage effect
    IEnumerator DamageEffectCoroutine()
    {
        // Smoothly increase the alpha to 0.5f
        float targetAlpha = damageEffectAlpha;
        float duration = damageEffectDuration;
        float elapsedTime = 0f;

        Color color = damageFillImage.color;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Lerp(0f, targetAlpha, elapsedTime / duration);
            damageFillImage.color = color;
            yield return null;
        }

        // Smoothly decrease the alpha back to 0
        elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Lerp(targetAlpha, 0f, elapsedTime / duration);
            damageFillImage.color = color;
            yield return null;
        }

        // Ensure the alpha is fully reset to 0
        color.a = 0f;
        damageFillImage.color = color;
    }

    // Player death handling
    void PlayerDeath()
    {
        StartCoroutine(Co_PlayerDeath());
    }

    IEnumerator Co_PlayerDeath()
    {
        yield return new WaitForEndOfFrame();

        playerWeaponSystem.ExitOutOfAiming();

        yield return new WaitForEndOfFrame();

        // Handle player death
        Debug.Log("Player has died.");
        animator.SetBool("Dead", true);

        //Trigger Event
        OnPlayerDeath?.Invoke();
    }

    // Reset player animator state
    public void ResetAnimator()
    {
        // Reset animator state
        animator.SetBool("Dead", false);
        animator.SetFloat("Velocity", 0f);
    }

    //Reset Player and animations
    public void ResetPlayer()
    {
        // Reset player state
        isAttacking = false;
        isRolling = false;

        //Reset animator state
        ResetAnimator();

        // Reset health
        health.ResetHealth();
    }

    //Chek if player is dead
    public bool IsDead()
    {
        return health.IsDead;
    }

    public void SetFusebox(Fusebox fuseBox)
    {
        currentFuseboxInRange = fuseBox;
        fuseBoxInRange = true;
    }

    public void RemoveFusebox()
    {
        currentFuseboxInRange = null;
        fuseBoxInRange = false;
    }

    float lastInteractionTime = 0f;
    public void SetLastInteractionTime(float time)
    {
        lastInteractionTime = time;
    }

    public float GetLastInteractionTime()
    {
        return lastInteractionTime;
    }
    
    /// <summary>
    /// Reset everything for a completely fresh new game
    /// </summary>
    void ResetForNewGame()
    {
        Debug.Log("[Player] ResetForNewGame - clearing inventory and resetting stats");
        
        // Reset health and animator
        ResetPlayer();
        
        // Clear inventory completely
        if (inventory != null)
        {
            inventory.ClearInventory();
        }
        
        // Reset weapon state
        if (playerWeaponSystem != null)
        {
            playerWeaponSystem.currentAmmo = 0;
            playerWeaponSystem.weaponIsEnabled = false;
        }
        
        // Reset stability to max
        if (playerStability != null)
        {
            playerStability.stability = playerStability.maxStability;
        }
        
        // Reset map flag
        hasMap = false;
        PlayerPrefs.SetInt("HasMap", 0);
    }
}
