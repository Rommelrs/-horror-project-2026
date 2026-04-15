using System.Collections;
using System.Collections.Generic;
using ToolBox.Pools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] InputActionReference lightAttackInput;
    [SerializeField] TrailRenderer trail;
    [SerializeField] GameObject[] hitImpactParticles;
    [SerializeField] AudioClip[] attackSFX;
    [SerializeField] AudioClip[] fleshHitSFX;
    [SerializeField] float animationSmoothness = 5f;

    [Header("Light Attack")]
    [SerializeField] int lightDamage = 20;
    [SerializeField] float lightAttackDuration = 0.35f;
    [SerializeField] float lightAttackForwardForce = 1f;
    [SerializeField] float lightStaminaCost = 5f;
    [SerializeField] AudioClip[] lightSlashSounds;
    [SerializeField] float lightCameraShakeIntensity = 0.2f;
    [SerializeField] float lightCameraShakeDuration = 0.2f;

    [Header("Hitbox")]
    [SerializeField] float hitBoxRadius = 1;
    [SerializeField] Vector3 hitBoxOffset = new Vector3(0, 0, 0);
    [SerializeField] float hitBoxDistance = 1;

    bool canAttack = true;
    Animator anim;
    AudioSource audioSource;
    Player player;
    PlayerRoll playerRoll;
    CharacterController characterController;
    PlayerStamina playerStamina;
    PlayerEventHandler playerEventHandler;
    bool attackInitiated = false;
    int lightAttackIndex = 0;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        anim = GetComponentInChildren<Animator>();
        player = GetComponent<Player>();
        playerRoll = GetComponent<PlayerRoll>();
        characterController = GetComponent<CharacterController>();
        playerStamina = GetComponent<PlayerStamina>();
        playerEventHandler = GetComponentInChildren<PlayerEventHandler>();
    }

    private void OnDestroy()
    {
        if (playerRoll != null)
            playerRoll.OnRollStarted -= OnRollStarted;

        if (playerEventHandler != null)
        {
            playerEventHandler.OnAttack -= Attack;
            playerEventHandler.OnStartSlash -= StartSlash;
            playerEventHandler.OnEndSlash -= EndSlash;
        }
    }

    private void Start()
    {
        if (playerRoll != null)
            playerRoll.OnRollStarted += OnRollStarted;

        if (playerEventHandler != null)
        {
            playerEventHandler.OnAttack += Attack;
            playerEventHandler.OnStartSlash += StartSlash;
            playerEventHandler.OnEndSlash += EndSlash;
        }
    }

    private void Update()
    {
        //Check if the player is Dead
        if (player.IsDead())
            return;

        //Check if it's already game over or won
        if (LevelManager.instance != null && (LevelManager.instance.isGameOver || LevelManager.instance.isGameWon))
            return;

        HandleAttack();
        HandleForwardMovement();
    }

    //Handle forward movement during attack
    void HandleForwardMovement()
    {
        //Movement
        if (attackInitiated)
        {
            Vector3 moveDirection;

            moveDirection = transform.forward * lightAttackForwardForce;

            //Apply forward movement
            characterController.Move(moveDirection * Time.deltaTime);
        }
    }

    //Light Attack Behaviour
    void HandleAttack()
    {
        float inputValue = lightAttackInput.action.ReadValue<float>();

        if (GameManager.IsPaused)
            return;

        if (Player.instance.isRolling || Player.instance.isScared)
            return;

        //Check if input is pressed and player has enough stamina
        if (inputValue > 0.1f && canAttack && ((playerStamina != null && playerStamina.Stamina >= lightStaminaCost) || playerStamina == null))
        {
            canAttack = false;
            player.isAttacking = true;
            StartCoroutine(Co_ResetAttack(lightAttackDuration));

            //Consume stamina
            if(playerStamina != null)
                playerStamina.UseStamina(lightStaminaCost);

            //Attack
            lightAttackIndex++;
            if (lightAttackIndex >= 3) lightAttackIndex = 0;

            //Play Attack SFX
            if (attackSFX.Length > 0)
            {
                int randomIndex = Random.Range(0, attackSFX.Length);
                audioSource.PlayOneShot(attackSFX[randomIndex]);
            }

            anim.SetInteger("LightAttackIndex", lightAttackIndex);
            anim.SetTrigger("Attack");
        }
    }

    void OnRollStarted()
    {
        //Check if the player is attacking then reset attacking state
        if (player.isAttacking)
        {
            //Reset the attack
            player.isAttacking = false;
            if(trail != null) trail.emitting = false;
            attackInitiated = false;
        }
    }

    //Light Attack Behaviour
    void Attack()
    {
        RaycastHit[] hit = Physics.SphereCastAll(GetHitboxCenter(), hitBoxRadius, transform.forward.normalized, hitBoxDistance);
        foreach (var hitObject in hit)
        {
            //Skip if the hit object is null
            if (hitObject.collider == null) { continue; }

            //Check if the hit object is a child of the player
            if (hitObject.collider.gameObject == transform.gameObject) { continue; }

            IDamagable damagable = hitObject.collider.GetComponent<IDamagable>();
            Health health = hitObject.collider.GetComponent<Health>();

            if (damagable == null)
                damagable = hitObject.collider.GetComponentInParent<IDamagable>();

            if (health == null)
                health = hitObject.collider.GetComponentInParent<Health>();

            if (damagable != null)
            {
                Quaternion hitRotation;
                Vector3 damagePoint;

                // Calculate hit rotation and damage point
                if (hitObject.point != Vector3.zero)
                {
                    // Use the actual hit point from the RaycastHit
                    hitRotation = Quaternion.LookRotation(hitObject.normal);

                    //Set Damgae Direction
                    if (health != null)
                        health.SetDamageDirection(hitObject.normal);

                    //Set Damage Point
                    damagePoint = hitObject.point;
                }
                else
                {
                    // Fallback to a calculated hit point if the hitObject.point is invalid
                    Vector3 hitBoxCenter = GetHitboxCenter() + (transform.forward * hitBoxDistance);
                    Vector3 enemyPosition = hitObject.collider.transform.position;
                    enemyPosition.y = hitBoxCenter.y; // Align y position with the hitbox center
                    Vector3 direction = enemyPosition - hitBoxCenter;
                    hitRotation = Quaternion.LookRotation(direction);

                    //Set Damage Direction
                    if (health != null)
                        health.SetDamageDirection(direction);

                    //Set Damage Point
                    damagePoint = hitBoxCenter + direction.normalized * hitBoxRadius;
                }

                //Damge Impuse Force
                if (health != null)
                    health.DamageImpactForce = 1f;

                // Apply damage
                damagable.Damage(lightDamage);

                //Play Flesh Hit Sound
                PlayFleshHitSound();

                //Spawn Hit Impact Particle
                SpawnHitImpactParticle(damagePoint, hitRotation);
            }
        }
    }

    //Start the slash trail
    void StartSlash()
    {
        //Check if the player is attacking
        if (player.isAttacking)
        {
            attackInitiated = true;

            //Play random light slash sound
            if (lightSlashSounds.Length > 0)
            {
                int randomIndex = Random.Range(0, lightSlashSounds.Length);
                audioSource.PlayOneShot(lightSlashSounds[randomIndex]);
            }

            //Start emmiting the trail
            if (trail != null) trail.emitting = true;
        }
    }

    //End the slash trail
    void EndSlash()
    {
        if (trail != null) trail.emitting = false;
        attackInitiated = false;
    }

    //Reset Attack after reset delay
    IEnumerator Co_ResetAttack(float resetDelay)
    {
        yield return new WaitForSeconds(resetDelay);
        canAttack = true;
        player.isAttacking = false;
    }

    //Get the hitbox center position
    Vector3 GetHitboxCenter()
    {
        return transform.position + (transform.forward * hitBoxOffset.z)
             + (transform.up * hitBoxOffset.y)
             + (transform.right * hitBoxOffset.x);
    }

    //Spawn the hit impact particle
    void SpawnHitImpactParticle(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (hitImpactParticles.Length > 0)
        {
            //Instantiate hit impact particle
            int randomIndex = Random.Range(0, hitImpactParticles.Length);
            GameObject hitImpact = hitImpactParticles[randomIndex].Reuse(spawnPosition, spawnRotation);
            hitImpact.GetComponent<ParticleSystem>().Play();
        }
    }

    //Play the flesh hit sound
    void PlayFleshHitSound()
    {
        if (fleshHitSFX.Length > 0)
        {
            int randomIndex = Random.Range(0, fleshHitSFX.Length);
            audioSource.PlayOneShot(fleshHitSFX[randomIndex]);
        }
    }

    //Debugging
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetHitboxCenter() + (transform.forward * hitBoxDistance), hitBoxRadius);
    }
}
