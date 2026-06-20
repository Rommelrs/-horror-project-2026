using System.Collections;
using System.Collections.Generic;
using ToolBox.Pools;
using UnityEngine;
using UnityEngine.AI;

public class BagBearerEnemy : Enemy
{
    public EnemyReloadState enemyReloadState;

    // Shared across all BagBearerEnemies - prevents simultaneous throws
    static float lastThrowTime = 0f;
    static float throwStaggerDelay = 1f;

    [Header("Bag Bearer Setup")]
    [SerializeField] Projectile projectilePrefab;
    [SerializeField] float positionOffset = 0.8f;
    public BagBearerReloadGroup []bagBearerReloadGroup;
    
    [Header("Spacing Settings")]
    [SerializeField] float separationDistance = 2f; // Minimum distance to keep from other enemies
    [SerializeField] float separationForce = 1f; // How strongly to push away from others
    [SerializeField] LayerMask enemyLayer; // Layer mask for detecting other enemies
    
    // Performance optimization - throttle expensive checks
    private float lastSeparationCheckTime = 0f;
    private float separationCheckInterval = 0.2f; // Check every 0.2 seconds instead of every frame
    private float lastLoSCheckTime = 0f;
    private float loSCheckInterval = 0.15f; // Check LoS every 0.15 seconds
    private bool cachedHasLoS = false;
    private bool cachedSpawnPointsClear = false;

    [System.Serializable]
    public struct BagBearerReloadGroup
    {
        public int reloadIndex;
        public GameObject reloadObject;
        public Transform projectileSpawnPoint;
        public bool isReloaded;
        public float weakpointShowDelay;
        public float reloadDurationStart;
        public float reloadDurationEnd;
    }

    public override void Awake()
    {
        base.Awake();

        enemyReloadState = new EnemyReloadState(this, stateMachine);
    }

    public override void Start()
    {
        base.Start();

        OnChaseStarted.AddListener(EnemyChaseStateStarted);
        OnAttackStarted.AddListener(EnemyAttackStateStarted);
        
        // Disable NavMeshAgent movement for Fixed enemies
        if (stats.enemyType == EnemyType.Fixed && agent != null && agent.isOnNavMesh)
        {
            agent.updatePosition = false; // Don't move
            agent.updateRotation = true;  // Allow rotation
            agent.isStopped = true;
        }

        // Ensure reload objects match their isReloaded state on spawn
        for (int i = 0; i < bagBearerReloadGroup.Length; i++)
        {
            if (bagBearerReloadGroup[i].reloadObject != null)
            {
                bagBearerReloadGroup[i].reloadObject.SetActive(bagBearerReloadGroup[i].isReloaded);
            }
        }

        if (CheckIfEnemyNeedReload() && stateMachine.CurrentState != enemyReloadState)
        {
            stateMachine.ChangeState(enemyReloadState);
        }

        //if (enemy.health.isDamageByWeakpointHit)
    }

    void LateUpdate()
    {
        // Keep Fixed enemies stationary
        if (stats.enemyType == EnemyType.Fixed)
        {
            // Allow movement during knockback
            if (stateMachine.CurrentState == enemyKnockbackState)
                return;
            
            // Force velocity to zero so animation stays idle
            if (agent != null && agent.isOnNavMesh)
            {
                agent.velocity = Vector3.zero;
                agent.isStopped = true;
            }
        }
    }
    
    public override void OnDestroy()
    {
        base.OnDestroy();

        OnChaseStarted.RemoveListener(EnemyChaseStateStarted);
        OnAttackStarted.RemoveListener(EnemyAttackStateStarted);
        
        // Release attack token when destroyed
        if (EnemyAttackCoordinator.Instance != null)
        {
            EnemyAttackCoordinator.Instance.OnEnemyDestroyed(this);
        }
    }
    

    public override void CheckLeaveCondition(EnemyState currentState)
    {
        //Currently in Idle State
        if (currentState == idleState)
        {
            //Ready to Leave
            if (currentState.canLeave)
            {
                if (stats.enemyType == EnemyType.Aggressive)
                {
                    //Aggressive Enemy Type
                    stateMachine.ChangeState(chaseState);
                }
                else if (stats.enemyType == EnemyType.Wandering)
                {
                    //Wandering Enemy Type
                    // Detect player in vision range
                    if (PlayerInRange(stats.visionRadius))
                    {
                        stateMachine.ChangeState(chaseState);
                    }
                    else
                    {
                        stateMachine.ChangeState(enemyWanderState);
                    }
                }
                else if (stats.enemyType == EnemyType.Fixed)
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
        if (currentState == enemyWanderState)
        {
            if (stats.enemyType == EnemyType.Wandering || stats.enemyType == EnemyType.Aggressive)
            {
                //If within attack range
                if (PlayerInRange(stats.attackRange))
                {
                    stateMachine.ChangeState(attackState);
                }

                //If within vision radius
                if (PlayerInRange(stats.visionRadius))
                {
                    stateMachine.ChangeState(chaseState);
                }
            }
        }

        //Currently in EnemyChaseState
        if (currentState == chaseState)
        {
            // Apply separation from other enemies (throttled)
            if (Time.time - lastSeparationCheckTime > separationCheckInterval)
            {
                ApplySeparation();
                lastSeparationCheckTime = Time.time;
            }
            
            if (currentState.canLeave)
            {
                // If within attack range AND has line of sight AND spawn points are clear, enter attack state
                if (PlayerInRange(stats.attackRange))
                {
                    // Throttle expensive LoS and spawn point checks
                    if (Time.time - lastLoSCheckTime > loSCheckInterval)
                    {
                        cachedHasLoS = HasLineOfSightToPlayer();
                        cachedSpawnPointsClear = HasClearSpawnPoints();
                        lastLoSCheckTime = Time.time;
                    }
                    
                    if (cachedHasLoS && cachedSpawnPointsClear)
                    {
                        // Stagger throws - don't attack if another BagBearer threw recently
                        if (Time.time >= lastThrowTime + throwStaggerDelay)
                        {
                            stateMachine.ChangeState(attackState);
                        }
                        // Otherwise stay in chase and keep trying
                    }
                    // Otherwise stay in chase to reposition for clear shot and clear spawn points
                }
            }
        }

        //Currently in AttackState
        if (currentState == attackState)
        {
            // Throttle expensive LoS checks in attack state too
            if (Time.time - lastLoSCheckTime > loSCheckInterval)
            {
                cachedHasLoS = HasLineOfSightToPlayer();
                cachedSpawnPointsClear = HasClearSpawnPoints();
                lastLoSCheckTime = Time.time;
            }
            
            // If lost line of sight OR spawn points blocked, go back to chase to reposition
            if (currentState.canLeave && (!cachedHasLoS || !cachedSpawnPointsClear))
            {
                // Release token when leaving attack state
                if (EnemyAttackCoordinator.Instance != null)
                {
                    EnemyAttackCoordinator.Instance.ReleaseToken(this);
                }
                stateMachine.ChangeState(chaseState);
                return;
            }
            
            
            if (stats.enemyType == EnemyType.Wandering || stats.enemyType == EnemyType.Aggressive)
            {
                if (currentState.canLeave && !PlayerInRange(stats.attackRange))
                {
                    // Release token when leaving attack state
                    if (EnemyAttackCoordinator.Instance != null)
                    {
                        EnemyAttackCoordinator.Instance.ReleaseToken(this);
                    }
                    stateMachine.ChangeState(chaseState);
                    return;
                }
            }
            else if(stats.enemyType == EnemyType.Fixed)
            {
                if (currentState.canLeave && !PlayerInRange(stats.attackRange))
                {
                    // Release token when leaving attack state
                    if (EnemyAttackCoordinator.Instance != null)
                    {
                        EnemyAttackCoordinator.Instance.ReleaseToken(this);
                    }
                    stateMachine.ChangeState(idleState);
                    return;
                }
            }

            if (CheckIfEnemyNeedReload())
            {
                // Release token when going to reload
                if (EnemyAttackCoordinator.Instance != null)
                {
                    EnemyAttackCoordinator.Instance.ReleaseToken(this);
                }
                stateMachine.ChangeState(enemyReloadState);
                return;
            }
        }
    }

    public override void TakeDamage(int dmgValue)
    {
        if (health.IsDead) return;

        if ((stateMachine.CurrentState != enemyReloadState && stateMachine.CurrentState != attackState) || health.isDamageByWeakpointHit)
        {
            // Release token if being knocked back from attack state
            if (stateMachine.CurrentState == attackState && EnemyAttackCoordinator.Instance != null)
            {
                EnemyAttackCoordinator.Instance.ReleaseToken(this);
            }
            
            //Knockback State
            Vector3 playerPos = Player.instance.transform.position;
            playerPos.y = transform.position.y;
            Vector3 damageDirection = transform.position - playerPos;
            this.damageDirection = damageDirection;

            if (health.isDamageByWeakpointHit)
                this.damageForceMultiplier = Player.instance.playerWeaponSystem.weakpointHitEnemyKnockbackMultiplier;
            else
                this.damageForceMultiplier = 1f;

            // Fixed enemies: allow knockback state for weakpoint hits, otherwise just play animation
            if(stats.enemyType != EnemyType.Fixed || health.isDamageByWeakpointHit)
                stateMachine.ChangeState(enemyKnockbackState);
            else
                anim.SetTrigger("Knockback");

            PlaySoundEffect(stats.takeDamageSFX);
        }
        else
        {
            //Trigger knockback Animation
            anim.SetTrigger("Knockback");

            //Only Play SFX
            PlaySoundEffect(stats.takeDamageSFX);
        }
    }

    public void EnemyChaseStateStarted()
    {
        if (CheckIfEnemyNeedReload() && stateMachine.CurrentState != enemyReloadState)
        {
            stateMachine.ChangeState(enemyReloadState);
        }
    }

    public void EnemyAttackStateStarted()
    {
        if (CheckIfEnemyNeedReload() && stateMachine.CurrentState != enemyReloadState)
        {
            stateMachine.ChangeState(enemyReloadState);
        }
    }

    public bool CheckIfEnemyNeedReload()
    {
        bool needReloading = true;
        foreach (BagBearerReloadGroup item in bagBearerReloadGroup)
        {
            if (item.isReloaded)
                needReloading = false;
        }

        return needReloading;
    }

    private bool HasLineOfSightToPlayer()
    {
        if (Player.instance == null) return false;

        Vector3 rayStart = transform.position + Vector3.up * 1.5f;
        Vector3 playerCenter = Player.instance.transform.position + Vector3.up * 1f;
        Vector3 directionToPlayer = playerCenter - rayStart;
        RaycastHit hit;
        
        int layerMask = ~LayerMask.GetMask("Hitbox");
        
        if (Physics.Raycast(rayStart, directionToPlayer.normalized, out hit, stats.attackRange, layerMask))
        {
            return hit.collider.GetComponentInParent<Player>() != null;
        }
        
        return false;
    }
    
    private bool HasClearSpawnPoints()
    {
        // Check if projectile spawn points have clear space (not blocked by walls)
        foreach (BagBearerReloadGroup reloadGroup in bagBearerReloadGroup)
        {
            if (reloadGroup.isReloaded && reloadGroup.projectileSpawnPoint != null)
            {
                Vector3 spawnPoint = reloadGroup.projectileSpawnPoint.position;
                
                // Check in the direction of the player for obstacles very close to spawn point
                Vector3 directionToPlayer = (Player.instance.transform.position - spawnPoint).normalized;
                
                // Check for walls/obstacles within 1.5m of the spawn point in player direction
                RaycastHit hit;
                int layerMask = ~LayerMask.GetMask("Hitbox", "Enemy");
                
                if (Physics.Raycast(spawnPoint, directionToPlayer, out hit, 1.5f, layerMask))
                {
                    // If we hit something that's not the player, spawn point is blocked
                    if (hit.collider.GetComponentInParent<Player>() == null)
                    {
                        return false;
                    }
                }
            }
        }
        
        return true; // All spawn points are clear
    }
    private void ApplySeparation()
    {
        // Find nearby enemies
        Collider[] nearbyEnemies = Physics.OverlapSphere(transform.position, separationDistance, enemyLayer);
        
        Vector3 separationVector = Vector3.zero;
        int neighborCount = 0;
        
        foreach (Collider enemyCollider in nearbyEnemies)
        {
            // Skip self
            if (enemyCollider.transform == transform)
                continue;
            
            // Calculate direction away from this enemy
            Vector3 directionAway = transform.position - enemyCollider.transform.position;
            float distance = directionAway.magnitude;
            
            if (distance > 0 && distance < separationDistance)
            {
                // Weight by distance (closer enemies push harder)
                separationVector += directionAway.normalized / distance;
                neighborCount++;
            }
        }
        
        // Apply separation to NavMeshAgent if we have neighbors too close
        if (neighborCount > 0 && agent != null && agent.isOnNavMesh)
        {
            separationVector = separationVector.normalized * separationForce;
            agent.velocity += separationVector;
        }
    }

    //Attack successfully called from Animation Event
    public override void AttackHit()
    {
        if (Player.instance == null) return;

        for (int i = 0; i < bagBearerReloadGroup.Length; i++)
        {
            if (bagBearerReloadGroup[i].isReloaded == true)
            {
                //Spawn a projectile and throw at the player
                lastThrowTime = Time.time;
                Vector3 direction = transform.position - Player.instance.transform.position;
                Vector3 targetPosition = Player.instance.transform.position + direction.normalized * positionOffset;

                GameObject projectileObj = projectilePrefab.gameObject.Reuse(bagBearerReloadGroup[i].projectileSpawnPoint.position, Quaternion.identity);
                Projectile projectile = projectileObj.GetComponent<Projectile>();
                projectile.ShootProjectile(targetPosition, this);

                bagBearerReloadGroup[i].isReloaded = false;
                bagBearerReloadGroup[i].reloadObject.SetActive(false);

                return;
            }
        }         
    }

}
