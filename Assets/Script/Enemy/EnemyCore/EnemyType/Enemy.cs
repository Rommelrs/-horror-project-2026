using System.Collections;
using System.Collections.Generic;
using ToolBox.Pools;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class Enemy : MonoBehaviour, IPoolable
{
    public Stats stats;
    public Health health;
    public NavMeshAgent agent;
    public Rigidbody rb;
    public Animator anim;
    public EnemyWeakpoint enemyWeakpoint;
    public Collider deathCollider;
    public string currentState;
    public EnemyStateMachine stateMachine;
    public UnityEvent OnEnemyDied;
    public UnityEvent OnChaseStarted;
    public UnityEvent OnAttackStarted;

    //States
    public EnemyIdleState idleState;
    public EnemyWanderState enemyWanderState;
    public EnemyChaseState chaseState;
    public EnemyAttackState attackState;
    public EnemyDeathState deathState;
    public EnemyKnockBackState enemyKnockbackState;
    public EnemyInvestigateState investigateState;

    AudioSource audioSource;
    EnemyEventHandler enemyEventHandler;
    float currentVelocity;

    [HideInInspector] public Vector3 damageDirection;
    [HideInInspector] public float damageForceMultiplier;
    [HideInInspector] public bool chaseStartDelayEnabled = true;
    bool pauseEnemyState = false;

    public bool playerDetected = false;
    public UnityEvent onPlayerDetected;

    // Containment Zone
    private EnemyContainmentZone containmentZone;

    public virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();
        enemyEventHandler = GetComponentInChildren<EnemyEventHandler>();

        stateMachine = new EnemyStateMachine();

        // Configure AudioSource for 3D spatial audio
        if (audioSource != null)
        {
            audioSource.spatialBlend = 1f;  // Full 3D
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 5f;
            audioSource.maxDistance = 50f;
            audioSource.volume = 1f;
        }

        idleState = new EnemyIdleState(this, stateMachine);
        enemyWanderState = new EnemyWanderState(this, stateMachine);
        chaseState = new EnemyChaseState(this, stateMachine);
        attackState = new EnemyAttackState(this, stateMachine);
        deathState = new EnemyDeathState(this, stateMachine);
        enemyKnockbackState = new EnemyKnockBackState(this, stateMachine);
        investigateState = new EnemyInvestigateState(this, stateMachine);
    }

    public virtual void Start()
    {
        //Initialize Start State
        stateMachine.Initialize(idleState);

        //Subscribe to events
        health.OnDeath.AddListener(Die);
        health.OnDamageTaken += TakeDamage;
        
        if (enemyEventHandler != null)
        {
            enemyEventHandler.OnAttack += AttackHit;
            enemyEventHandler.OnFootstep += PlayFootstep;
        }

        if (randomSFXCR != null) StopCoroutine(randomSFXCR);
        randomSFXCR = StartCoroutine(Co_PlayRandomSound());
    }

    public virtual void OnDestroy()
    {
        //unsubscribe to events
        health.OnDeath.RemoveListener(Die);
        health.OnDamageTaken -= TakeDamage;
        
        if (enemyEventHandler != null)
        {
            enemyEventHandler.OnAttack -= AttackHit;
            enemyEventHandler.OnFootstep -= PlayFootstep;
        }
    }

    void Update()
    {
        if (!pauseEnemyState)
        {
            //Handle State Tick
            stateMachine.CurrentState.Update();
            
            //Update State string
            string newState = stateMachine.CurrentState.ToString();
            if (currentState != newState)
            {
                currentState = newState;
            }
        }
        else
            currentState = "PAUSED!";

        //Handle Animation Behaviour
        HandleAnimation();
    }

    public virtual void CheckLeaveCondition(EnemyState currentState)
    {
        //Currently in Idle State
        if (currentState == idleState)
        {
            //Ready to Leave
            if (currentState.canLeave)
            {
                if(stats.enemyType == EnemyType.Aggressive)
                {
                    //Aggressive Enemy Type
                    stateMachine.ChangeState(chaseState);
                }
                else if(stats.enemyType == EnemyType.Wandering)
                {
                    //Wandering Enemy Type
                    // Detect player in vision range
                    if (EnemyDetectionHelper.CheckDetection(this)) { stateMachine.ChangeState(chaseState);
                    }
                    else
                    {
                        stateMachine.ChangeState(enemyWanderState);
                    }
                }
                else if(stats.enemyType == EnemyType.Fixed)
                {
                    // Detect player in vision range
                    if (PlayerInRange(stats.attackRange))
                    {
                        stateMachine.ChangeState(attackState);
                    }
                }
            }
        }

        //Currently in Wander State
        if(currentState == enemyWanderState)
        {
            if (stats.enemyType == EnemyType.Wandering || stats.enemyType == EnemyType.Aggressive)
            {
                //If within attack range
                if (PlayerInRange(stats.attackRange))
                {
                    stateMachine.ChangeState(attackState);
                }

                //If within vision radius
                if (EnemyDetectionHelper.CheckDetection(this)) { stateMachine.ChangeState(chaseState);
                }
            }
        }

        //Currently in EnemyChaseState
        if(currentState == chaseState)
        {
            if (currentState.canLeave)
            {
                // If within attack range
                if (PlayerInRange(stats.attackRange))
                {
                    stateMachine.ChangeState(attackState);
                }
            }
        }

        //Currently in AttackState
        if(currentState == attackState)
        {
            if (stats.enemyType == EnemyType.Wandering || stats.enemyType == EnemyType.Aggressive)
            {
                if (currentState.canLeave && !PlayerInRange(stats.attackRange))
                {
                    stateMachine.ChangeState(chaseState);
                    return;
                }
            }
            else if(stats.enemyType == EnemyType.Fixed)
            {
                if (currentState.canLeave && !PlayerInRange(stats.attackRange))
                {
                    stateMachine.ChangeState(idleState);
                    return;
                }
            }         
        }
    }

    private void HandleAnimation()
    {
        //Smothly Lerp to new Velocity for smoother animation transition
        currentVelocity = Mathf.Lerp(currentVelocity, (agent.velocity.magnitude >= 0.1) ? agent.velocity.magnitude : 0, stats.animationSmoothness * Time.deltaTime);
        anim.SetFloat("Velocity", currentVelocity);
        
        // Also set Speed parameter (0 = idle, 1 = walk, 2 = run)
        float speed = 0f;
        if (currentVelocity > 0.1f)
        {
            // Walking/running - map velocity to 0-2 range
            speed = Mathf.Clamp(currentVelocity / stats.movementSpeed * 2f, 0f, 2f);
        }
        anim.SetFloat("Speed", speed);
    }

    public bool PlayerInRange(float range)
    {
        if (Player.instance == null) return false;
        return Vector3.Distance(transform.position, Player.instance.transform.position) < range;
    }

    //Check is player is inside the enemy Field of View
    public virtual void AlertToSound(Vector3 soundPosition)
    {
        // Don't alert if can't hear gunshots
        if (!stats.canHearGunshots)
            return;

        // Don't alert if already chasing or attacking player
        if (playerDetected || stateMachine.CurrentState == chaseState || stateMachine.CurrentState == attackState)
            return;

        // Don't alert if dead
        if (health != null && health.IsDead)
            return;

        investigateState.SetTargetPosition(soundPosition);
        stateMachine.ChangeState(investigateState);
    }

    public bool PlayerInFOV()
    {
        if (Player.instance == null) return false;

        Vector3 targetDir = Player.instance.transform.position - this.transform.position;
        float angleToPlayer = (Vector3.Angle(targetDir, this.transform.forward));

        if (angleToPlayer >= -stats.enemyFOV && angleToPlayer <= stats.enemyFOV) // 180? FOV
        {
            return true;
        }

        return false;
    }

    public virtual void TakeDamage(int dmgValue)
    {
        if (health.IsDead) return;

        //Knockback State
        Vector3 playerPos = Player.instance.transform.position;
        playerPos.y = transform.position.y;
        Vector3 damageDirection = transform.position - playerPos;
        this.damageDirection = damageDirection;

        if (health.isDamageByWeakpointHit)
            this.damageForceMultiplier = Player.instance.playerWeaponSystem.weakpointHitEnemyKnockbackMultiplier;
        else
            this.damageForceMultiplier = 1f;

        // All enemy types now get knocked back
        stateMachine.ChangeState(enemyKnockbackState);

        PlaySoundEffect(stats.takeDamageSFX);
    }

    //Attack successfully called from Animation Event
    public virtual void AttackHit()
    {
        if (GameManager.IsPaused) return;

        if (health.IsDead) return;

        if (PlayerInRange(stats.attackRange) && PlayerInFOV())
        {
            //Successful hit
            Player.instance.health.Damage(stats.damage);
        }
    }

    public void PlaySoundEffect(AudioClip[] audioClips)
    {
        if (audioClips != null && audioClips.Length > 0)
        {
            audioSource.PlayOneShot(audioClips[Random.Range(0, audioClips.Length)]);
        }
    }
    
    public void Play2DSoundEffect(AudioClip[] audioClips)
    {
        if (audioClips != null && audioClips.Length > 0)
        {
            AudioClip clipToPlay = audioClips[Random.Range(0, audioClips.Length)];
            float randomPitch = Random.Range(0.9f, 1.1f);
            
            // Create temporary GameObject for pitched audio playback
            GameObject tempAudio = new GameObject("TempAudio_" + clipToPlay.name);
            tempAudio.transform.position = Camera.main.transform.position;
            
            AudioSource tempSource = tempAudio.AddComponent<AudioSource>();
            tempSource.clip = clipToPlay;
            tempSource.volume = 2.0f;
            tempSource.pitch = randomPitch;
            tempSource.spatialBlend = 0f; // 2D sound
            tempSource.Play();
            
            Destroy(tempAudio, clipToPlay.length / randomPitch + 0.1f);
        }
    }

    // Called from Animation Event
    public void PlayFootstep()
    {
        AudioClip[] clips = GetFootstepSoundsForCurrentSurface();
        
        if (clips != null && clips.Length > 0)
        {
            AudioClip randomFootstep = clips[Random.Range(0, clips.Length)];
            audioSource.PlayOneShot(randomFootstep, stats.footstepVolume);
        }
    }
    
    // Called from Animation Event - plays guaranteed attack sound
    public void PlayThrowSound()
    {
        if (stats.guaranteedAttackSound != null)
        {
            audioSource.PlayOneShot(stats.guaranteedAttackSound);
        }
    }
    
    // Called from Animation Event - stops the audio source
    public void StopThrowSound()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    AudioClip[] GetFootstepSoundsForCurrentSurface()
    {
        // Raycast down from enemy position to detect ground  
        Vector3 rayOrigin = transform.position;
        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 1.5f))
        {
            // Check if the ground has a GroundType component
            GroundType groundType = hit.collider.GetComponent<GroundType>();
            
            if (groundType != null)
            {
                // Get EnemySound component if it exists (for surface-specific sounds)
                EnemySound enemySound = GetComponent<EnemySound>();
                if (enemySound == null)
                    enemySound = GetComponentInChildren<EnemySound>();
                
                if (enemySound != null)
                {
                    // Use EnemySound's surface detection (will check its surfaceFootstepSounds array)
                    return enemySound.GetFootstepSoundsForSurface(groundType.surfaceType);
                }
            }
        }

        // Fallback to Stats footstep sounds
        return stats.footstepSounds;
    }

    public bool IsPlayerLookingAtEnemy(Transform player, Transform enemy, float viewAngle = 30f)
    {
        Vector3 dirToEnemy = (enemy.position - player.position).normalized;

        float dot = Vector3.Dot(player.forward, dirToEnemy);

        // Convert angle to dot threshold
        float threshold = Mathf.Cos(viewAngle * Mathf.Deg2Rad);

        return dot >= threshold;
    }

    public void Die()
    {
        //Destory Enemy Weakpoint objects
        if (enemyWeakpoint != null)
        {
            enemyWeakpoint.DestorySpawnedWeakpoint();
        }

        //Knockback State
        Vector3 playerPos = Player.instance.transform.position;
        playerPos.y = transform.position.y;
        Vector3 damageDirection = transform.position - playerPos;
        this.damageDirection = damageDirection;

        if (health.isDamageByWeakpointHit)
            this.damageForceMultiplier = Player.instance.playerWeaponSystem.weakpointHitEnemyKnockbackMultiplier;
        else
        {
            this.damageForceMultiplier = 1f;
        }

        //Stop Playing Random SFX
        if (randomSFXCR != null)
            StopCoroutine(randomSFXCR);

        //Death State
        stateMachine.ChangeState(deathState);

        //Trigger Event
        OnEnemyDied?.Invoke();
    }  

    public virtual void OnPool()
    {
        //Reset Enemy State
        PauseEnemyState(false);

        //Reset Death Collider
        deathCollider.enabled = false;

        //Reset Spawned Weakpoints
        if (enemyWeakpoint != null) enemyWeakpoint.DestorySpawnedWeakpoint();

        //Reset Health
        health.ResetHealth();

        //Reset Chase Delay
        chaseStartDelayEnabled = true;

        //Initialize Start State
        if (stats.enemyType == EnemyType.Aggressive)
        {
            stateMachine.Initialize(chaseState);
        }
        else if (stats.enemyType == EnemyType.Wandering)
        {
            stateMachine.Initialize(idleState);
        }

        if (randomSFXCR != null) StopCoroutine(randomSFXCR);
        randomSFXCR = StartCoroutine(Co_PlayRandomSound());
    }

    public virtual void OnDepool()
    {
        deathCollider.enabled = false;

        anim.SetBool("Dead", false);

        //Destory Enemy Weakpoint objects
        if (enemyWeakpoint != null)
        {
            //Reset Spawned Weakpoints
            enemyWeakpoint.DestorySpawnedWeakpoint();
        }
    }

    public virtual void ResetEnemy()
    {
        pauseEnemyState = false;

        //Initialize Start State
        stateMachine.Initialize(idleState);
    }

    public void PauseEnemyState(bool value)
    {
        pauseEnemyState = value;
    }
    
    /// <summary>
    /// Change enemy type at runtime. Can be overridden by subclasses for special handling.
    /// </summary>
    public virtual void ChangeEnemyType(EnemyType newType)
    {
        EnemyType oldType = stats.enemyType;
        stats.enemyType = newType;
        
        // If type actually changed, adjust state accordingly
        if (oldType != newType)
        {
            // Determine appropriate state based on new type
            if (newType == EnemyType.Aggressive)
            {
                // Aggressive enemies should chase immediately
                if (stateMachine.CurrentState == idleState || stateMachine.CurrentState == enemyWanderState)
                {
                    stateMachine.ChangeState(chaseState);
                }
            }
            else if (newType == EnemyType.Wandering)
            {
                // Wandering enemies check if player is in range
                if (stateMachine.CurrentState == idleState)
                {
                    if (PlayerInRange(stats.visionRadius))
                    {
                        stateMachine.ChangeState(chaseState);
                    }
                    else
                    {
                        stateMachine.ChangeState(enemyWanderState);
                    }
                }
            }
            else if (newType == EnemyType.Fixed)
            {
                // Fixed enemies should return to idle if chasing/wandering
                if (stateMachine.CurrentState == chaseState || stateMachine.CurrentState == enemyWanderState)
                {
                    stateMachine.ChangeState(idleState);
                }
            }
        }
    }

    public void SetContainmentZone(EnemyContainmentZone zone)
    {
        containmentZone = zone;
    }

    public void OnPlayerLeftContainmentZone()
    {
        // If player leaves containment zone, stop chasing and return to idle/wander
        if (stateMachine.CurrentState == chaseState || stateMachine.CurrentState == attackState)
        {
            if (stats.enemyType == EnemyType.Wandering)
            {
                stateMachine.ChangeState(enemyWanderState);
            }
            else
            {
                stateMachine.ChangeState(idleState);
            }
        }
    }

    public bool IsInContainmentZone()
    {
        return containmentZone != null;
    }

    public EnemyContainmentZone GetContainmentZone()
    {
        return containmentZone;
    }

    Coroutine randomSFXCR;

    IEnumerator Co_PlayRandomSound()
    {
        while (true)
        {
            if (stats == null)
                yield break;

            if (stats.randomSFX == null || stats.randomSFX.Length <= 0)
                yield break;

            float waitTime = Random.Range(stats.randomMinDuration, stats.randomMaxDuration);
            yield return new WaitForSeconds(waitTime);

            PlaySoundEffect(stats.randomSFX);
        }
    }

    public virtual void OnDrawGizmos()
    {
        //Show Vision Radius 
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, stats.visionRadius);

        //Show Attack Radius 
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stats.attackRange);
    }
}

[System.Serializable]
public enum EnemyType
{
    Aggressive,
    Wandering,
    Custom,
    Fixed
}

[System.Serializable]
public enum AttackType
{
    Melee,
    Ranged
}






