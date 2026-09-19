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
    static float throwStaggerDelay = 0.4f;
    static BagBearerEnemy currentAttacker = null; // Only one can be in attack state at a time

    bool holdingPosition = false;
    public override bool HoldPositionDuringChase => holdingPosition;

    [Header("Bag Bearer Setup")]
    [SerializeField] Projectile projectilePrefab;
    [SerializeField] float positionOffset = 0.8f;
    public BagBearerReloadGroup []bagBearerReloadGroup;
    
    [Header("Spacing Settings")]
    [SerializeField] float separationDistance = 2f;
    [SerializeField] float separationForce = 1f;
    [SerializeField] LayerMask enemyLayer;

    [Header("Wall Avoidance")]
    [SerializeField] float wallAvoidanceDistance = 2.5f; // How far to sense walls
    [SerializeField] float wallAvoidanceForce = 2f;     // How hard to steer away
    
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
        // Wall avoidance runs in LateUpdate so it overrides EnemyChaseState's destination
        if (stateMachine.CurrentState == chaseState)
            ApplyWallAvoidance();

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
        
        // Release attack token and attacker slot when destroyed
        if (currentAttacker == this) currentAttacker = null;
        if (EnemyAttackCoordinator.Instance != null)
        {
            EnemyAttackCoordinator.Instance.OnEnemyDestroyed(this);
        }
    }
    
    /// <summary>
    /// Call this when changing enemy type (e.g. from Fixed to Aggressive)
    /// </summary>
    public override void ChangeEnemyType(EnemyType newType)
    {
        EnemyType oldType = stats.enemyType;
        stats.enemyType = newType;
        
        // Clean up any active reload state before changing type
        if (stateMachine.CurrentState == enemyReloadState)
        {
            // Force exit reload state to clean up coroutines and weakpoints
            enemyWeakpoint.DestorySpawnedWeakpoint();
            
            // Change to appropriate state based on new type
            if (newType == EnemyType.Fixed)
            {
                stateMachine.ChangeState(idleState);
            }
            else
            {
                stateMachine.ChangeState(chaseState);
            }
        }
        
        // If transitioning FROM Fixed to something else, unlock movement
        if (oldType == EnemyType.Fixed && newType != EnemyType.Fixed)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.updatePosition = true;  // Re-enable position updates
                agent.updateRotation = true;  // Keep rotation enabled
                agent.isStopped = false;      // Allow movement
            }
            
            // Force state change to chase if in idle state
            if (stateMachine.CurrentState == idleState)
            {
                if (newType == EnemyType.Aggressive)
                {
                    stateMachine.ChangeState(chaseState);
                }
                else if (newType == EnemyType.Wandering)
                {
                    // Wandering type - check if player is in range
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
        }
        // If transitioning TO Fixed from something else, lock movement
        else if (oldType != EnemyType.Fixed && newType == EnemyType.Fixed)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.updatePosition = false; // Don't move
                agent.updateRotation = true;  // Allow rotation
                agent.isStopped = true;       // Stop movement
            }
            
            // Fixed enemies should only be in idle or attack state
            if (stateMachine.CurrentState == chaseState || stateMachine.CurrentState == enemyWanderState)
            {
                stateMachine.ChangeState(idleState);
            }
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
                    // Only stop if not near a wall - otherwise keep repositioning
                    if (!IsNearWall())
                    {
                        holdingPosition = true;
                        if (agent != null && agent.isOnNavMesh)
                            agent.isStopped = true;
                    }

                    // Throttle expensive LoS and spawn point checks
                    if (Time.time - lastLoSCheckTime > loSCheckInterval)
                    {
                        cachedHasLoS = HasLineOfSightToPlayer();
                        cachedSpawnPointsClear = HasClearSpawnPoints();
                        lastLoSCheckTime = Time.time;
                    }
                    
                    if (cachedHasLoS && cachedSpawnPointsClear)
                    {
                        // Only enter attack if no other BagBearer is currently attacking
                        bool slotFree = currentAttacker == null || currentAttacker == this;
                        if (slotFree && Time.time >= lastThrowTime + throwStaggerDelay)
                        {
                            currentAttacker = this; // Claim the attack slot
                            holdingPosition = false;
                            if (agent != null && agent.isOnNavMesh)
                                agent.isStopped = false;
                            stateMachine.ChangeState(attackState);
                        }
                        else if (!slotFree)
                        {
                            // Slot taken by another BagBearer - keep moving to stay ready
                            holdingPosition = false;
                            if (agent != null && agent.isOnNavMesh)
                                agent.isStopped = false;
                        }
                        // Otherwise (slot free but stagger delay) hold position
                    }
                    else
                    {
                        // No clear shot - resume movement to reposition
                        holdingPosition = false;
                        if (agent != null && agent.isOnNavMesh)
                            agent.isStopped = false;
                    }
                }
                else
                {
                    // Not in range - make sure agent is moving
                    holdingPosition = false;
                    if (agent != null && agent.isOnNavMesh)
                        agent.isStopped = false;
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
                if (EnemyAttackCoordinator.Instance != null) EnemyAttackCoordinator.Instance.ReleaseToken(this);
                if (currentAttacker == this) currentAttacker = null;
                stateMachine.ChangeState(chaseState);
                return;
            }
            
            
            if (stats.enemyType == EnemyType.Wandering || stats.enemyType == EnemyType.Aggressive)
            {
                if (currentState.canLeave && !PlayerInRange(stats.attackRange))
                {
                    if (EnemyAttackCoordinator.Instance != null) EnemyAttackCoordinator.Instance.ReleaseToken(this);
                    if (currentAttacker == this) currentAttacker = null;
                    stateMachine.ChangeState(chaseState);
                    return;
                }
            }
            else if(stats.enemyType == EnemyType.Fixed)
            {
                if (currentState.canLeave && !PlayerInRange(stats.attackRange))
                {
                    if (EnemyAttackCoordinator.Instance != null) EnemyAttackCoordinator.Instance.ReleaseToken(this);
                    if (currentAttacker == this) currentAttacker = null;
                    stateMachine.ChangeState(idleState);
                    return;
                }
            }

            if (CheckIfEnemyNeedReload())
            {
                if (EnemyAttackCoordinator.Instance != null) EnemyAttackCoordinator.Instance.ReleaseToken(this);
                if (currentAttacker == this) currentAttacker = null;
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
            // Release token and attack slot if being knocked back from attack state
            if (stateMachine.CurrentState == attackState)
            {
                if (EnemyAttackCoordinator.Instance != null)
                    EnemyAttackCoordinator.Instance.ReleaseToken(this);

                if (currentAttacker == this)
                    currentAttacker = null;
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
        
        // Exclude Hitbox and Enemy layers so other enemies don't block LoS
        int layerMask = ~LayerMask.GetMask("Hitbox", "Enemy");
        
        if (Physics.Raycast(rayStart, directionToPlayer.normalized, out hit, stats.attackRange, layerMask))
        {
            // Hit something - only LoS if it's the player
            return hit.collider.GetComponentInParent<Player>() != null;
        }
        
        // Nothing hit = completely clear path to player
        return true;
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
    // Returns true if enemy is currently too close to a wall
    private bool IsNearWall()
    {
        int layerMask = LayerMask.GetMask("Default", "Wall");
        float checkDist = 1f; // Fixed 1m threshold - only truly near walls
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, direction, checkDist, layerMask))
                return true;
        }
        return false;
    }

    private void ApplyWallAvoidance()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        Vector3 avoidanceVector = Vector3.zero;
        int hitCount = 0;

        // Cast rays in 8 directions to detect nearby walls
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            RaycastHit hit;

            int layerMask = LayerMask.GetMask("Default", "Wall");
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, direction, out hit, wallAvoidanceDistance, layerMask))
            {
                float strength = 1f - (hit.distance / wallAvoidanceDistance);
                avoidanceVector += -direction * strength;
                hitCount++;
            }
        }

        if (hitCount > 0 && agent.isOnNavMesh)
        {
            // Use agent.Move to directly push position away from walls
            // This works on top of pathfinding without changing the destination
            avoidanceVector = avoidanceVector.normalized * wallAvoidanceForce * Time.deltaTime;
            agent.Move(avoidanceVector);

            // If stopped near wall, un-stop temporarily to reposition
            if (agent.isStopped)
            {
                holdingPosition = false;
                agent.isStopped = false;
            }
        }
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
        
        // Apply separation by offsetting the NavMeshAgent destination instead of velocity
        // (modifying velocity directly fights NavMesh pathfinding)
        if (neighborCount > 0 && agent != null && agent.isOnNavMesh && !agent.isStopped)
        {
            separationVector = separationVector.normalized * separationForce;
            Vector3 newDestination = agent.destination + separationVector;
            agent.SetDestination(newDestination);
        }
    }

    //Attack successfully called from Animation Event
    public override void AttackHit()
    {
        if (Player.instance == null) return;

        // If another BagBearer threw very recently, skip this throw to stagger attacks
        if (Time.time < lastThrowTime + throwStaggerDelay)
        {
            stateMachine.ChangeState(chaseState);
            return;
        }

        for (int i = 0; i < bagBearerReloadGroup.Length; i++)
        {
            if (bagBearerReloadGroup[i].isReloaded == true)
            {
                //Spawn a projectile and throw at the player
                lastThrowTime = Time.time;
                if (currentAttacker == this) currentAttacker = null; // Release slot after throwing
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
