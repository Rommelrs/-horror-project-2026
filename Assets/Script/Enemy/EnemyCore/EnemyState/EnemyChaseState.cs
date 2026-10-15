using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyChaseState : EnemyState
{
    float playerOutOfVisionDuration = 0f;

    float lookEndTime;
    Quaternion targetRotation;
    
    // Random offset to make enemies spread out instead of all taking same path
    private Vector3 destinationOffset;
    private float offsetRecalculateTime = 0f;
    private float offsetRecalculateInterval = 3f; // Recalculate offset every 3 seconds

    // Throttle SetDestination calls - calling this every single frame with a
    // constantly-shifting target (which happens whenever the player is moving)
    // spams the shared NavMesh pathfinding budget across all enemies and can
    // leave an agent's path query perpetually restarted before it ever resolves,
    // making the enemy play its run animation while never actually moving.
    private Vector3 lastDestinationTarget;
    private float lastSetDestinationTime = -999f;
    private const float destinationMoveThreshold = 0.75f;
    private const float destinationResendInterval = 0.2f;

    public EnemyChaseState(Enemy enemy, EnemyStateMachine sm) : base(enemy, sm) { }

    public override void Enter()
    {
        enemy.agent.isStopped = false;
        enemy.agent.speed = enemy.stats.movementSpeed;

        if (!enemy.agent.enabled)
            enemy.agent.enabled = true;

        if (!enemy.agent.isOnNavMesh)
        {
            enemy.agent.Warp(enemy.transform.position);      
        }

        //Set Agent speed
        enemy.agent.speed = enemy.stats.movementSpeed;

        lookEndTime = Time.time + enemy.stats.chaseStartLookDuration;
        
        // Only use advanced pathfinding if enabled
        if (enemy.stats.useAdvancedPathfinding)
        {
            // Generate initial random offset to spread out enemies
            GenerateRandomOffset();
            
            // Initialize offset recalculation time (stagger for optimized mode)
            if (enemy.stats.useOptimizedPathfinding)
            {
                // Stagger timing to avoid all enemies pathfinding at once
                offsetRecalculateTime = Time.time + Random.Range(offsetRecalculateInterval * 0.5f, offsetRecalculateInterval * 1.5f);
            }
            else
            {
                // Regular timing for non-optimized mode
                offsetRecalculateTime = Time.time + offsetRecalculateInterval;
            }
        }
        else
        {
            // Basic mode: no offset, chase directly
            destinationOffset = Vector3.zero;
        }

        queueLeader = null;
        nextQueueDecisionTime = 0f;

        // Force the first SetDestination call in Update to fire immediately
        lastSetDestinationTime = -999f;
        lastDestinationTarget = enemy.transform.position;

        //Trigger OnChaseStarted Event
        enemy.OnChaseStarted?.Invoke();

        if (enemy.chaseStartDelayEnabled)
            canLeave = false;
        else
            canLeave = true;
    }

    public override void Update()
    {
        enemy.CheckLeaveCondition(this);

        // CheckLeaveCondition may have synchronously changed state (e.g. into
        // EnemySidestepState, which disables the agent). Bail out instead of
        // continuing to run this state's stale logic against a state that's
        // no longer active this frame.
        if (stateMachine.CurrentState != this)
            return;

        if (Player.instance == null)
            return;

        if (Time.time < lookEndTime && enemy.chaseStartDelayEnabled)
        {
            if (!enemy.agent.isStopped)
                enemy.agent.isStopped = true;

            //Look at player
            Vector3 toPlayer = Player.instance.transform.position - enemy.transform.position;
            if (enemy.stats.chaseStartTurnSpeed > 0f)
            {
                // Fast turn: flat direction, constant angular speed, and the wait ends as soon as he is facing the player
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.0001f)
                {
                    targetRotation = Quaternion.LookRotation(toPlayer);
                    enemy.transform.rotation = Quaternion.RotateTowards(enemy.transform.rotation, targetRotation, enemy.stats.chaseStartTurnSpeed * Time.deltaTime);
                    if (Quaternion.Angle(enemy.transform.rotation, targetRotation) < 15f)
                        lookEndTime = Time.time;
                }
            }
            else
            {
                targetRotation = Quaternion.LookRotation(toPlayer);
                enemy.transform.rotation = Quaternion.Lerp(enemy.transform.rotation, targetRotation, enemy.stats.rotationSmoothness * Time.deltaTime);
            }
        }
        else
        {
            canLeave = true;
            enemy.chaseStartDelayEnabled = false;

            //Chase Player
            if (enemy.agent.enabled && enemy.agent.isOnNavMesh)
            {
                // Calculate distance once for all checks
                float distanceToPlayer = Vector3.Distance(enemy.transform.position, Player.instance.transform.position);
                
                // Dynamic speed based on distance
                if (enemy.stats.useDynamicChaseSpeed)
                {
                    if (distanceToPlayer >= enemy.stats.sprintDistanceThreshold)
                    {
                        // Far away - sprint
                        enemy.agent.speed = enemy.stats.movementSpeed * enemy.stats.sprintSpeedMultiplier;
                    }
                    else if (distanceToPlayer <= enemy.stats.slowDistanceThreshold)
                    {
                        // Very close - slow down
                        enemy.agent.speed = enemy.stats.movementSpeed * enemy.stats.slowSpeedMultiplier;
                    }
                    else
                    {
                        // Medium range - normal speed
                        enemy.agent.speed = enemy.stats.movementSpeed;
                    }
                }
                
                // Direct approach: no offset point around the player, just go for him (optionally lined up behind a teammate)
                if (enemy.stats.chaseDirectApproach)
                {
                    destinationOffset = Vector3.zero;
                    UpdateQueueDecision();
                }
                // Advanced pathfinding: recalculate offset periodically
                else if (enemy.stats.useAdvancedPathfinding)
                {
                    // Wall (or anything solid) between us and the player: an offset point around the player could be on the wrong
                    // side of it and the enemy would pace along the wall forever. Head for the player's real position instead and
                    // let the NavMesh path find the way around; re-roll the offset as soon as there is a clear line again.
                    if (!enemy.HasLineOfSightToPlayer())
                    {
                        destinationOffset = Vector3.zero;
                        offsetRecalculateTime = Time.time;
                    }
                    // If already very close to player, ignore offset and go direct
                    else if (distanceToPlayer <= enemy.stats.attackRange * 1.2f)
                    {
                        destinationOffset = Vector3.zero;
                    }
                    else if (Time.time > offsetRecalculateTime)
                    {
                        GenerateRandomOffset();
                        offsetRecalculateTime = Time.time + offsetRecalculateInterval;
                    }
                }
                // Basic pathfinding: always chase directly (no offset)
                else
                {
                    destinationOffset = Vector3.zero;
                }
                
                Vector3 targetPosition = Player.instance.transform.position + destinationOffset;

                // Lined up behind a teammate: aim for the spot just behind him instead of the player
                if (enemy.stats.chaseDirectApproach && queueLeader != null)
                    targetPosition = GetQueuePosition(queueLeader, targetPosition);

                // Check if in containment zone
                if (enemy.IsInContainmentZone())
                {
                    EnemyContainmentZone zone = enemy.GetContainmentZone();
                    
                    // If player is outside zone, don't chase outside
                    if (!zone.IsPlayerInZone())
                    {
                        // Stop at zone boundary instead
                        targetPosition = zone.GetClosestPointInZone(Player.instance.transform.position);
                    }
                    // If target position is outside zone, clamp it
                    else if (!zone.IsPointInZone(targetPosition))
                    {
                        targetPosition = zone.GetClosestPointInZone(targetPosition);
                    }
                }
                
                // Only re-issue SetDestination when the target has moved meaningfully
                // or enough time has passed, instead of every single frame - avoids
                // constantly restarting the async path query while the player moves.
                if (Vector3.Distance(targetPosition, lastDestinationTarget) > destinationMoveThreshold
                    || Time.time - lastSetDestinationTime > destinationResendInterval)
                {
                    enemy.agent.SetDestination(targetPosition);
                    lastDestinationTarget = targetPosition;
                    lastSetDestinationTime = Time.time;
                }

                // Don't un-stop if the enemy wants to hold position (e.g. BagBearer waiting for attack slot)
                if(enemy.agent.isStopped && !enemy.HoldPositionDuringChase)
                    enemy.agent.isStopped = false;
            }
            else
                enemy.agent.Warp(enemy.transform.position);

            if (enemy.stats.enemyType == EnemyType.Wandering)
            {
                bool playerInVision = false;
                
                // Check based on detection mode
                if (enemy.stats.detectionMode == DetectionMode.FOVCone)
                {
                    playerInVision = enemy.PlayerInRange(enemy.stats.visionRadius) && enemy.PlayerInFOV();
                }
                else // SphereRadius
                {
                    playerInVision = enemy.PlayerInRange(enemy.stats.visionRadius);
                }
                
                if (!playerInVision)
                {
                    playerOutOfVisionDuration += Time.deltaTime;
                    if (playerOutOfVisionDuration >= 5f)
                    {
                        enemy.chaseStartDelayEnabled = true;

                        //Switch to Wandering State
                        stateMachine.ChangeState(enemy.enemyWanderState);
                    }
                }
                else
                {
                    playerOutOfVisionDuration = 0f;
                }
            }
        }
    }
    
    // ---- Queueing behind a teammate (Direct Approach only)
    Enemy queueLeader;
    float nextQueueDecisionTime;
    static Enemy[] allEnemiesBuffer;

    /// <summary>The teammate this enemy is currently lined up behind (null when taking its own route).</summary>
    public Enemy QueueLeader => queueLeader;

    void UpdateQueueDecision()
    {
        // Drop the leader once he is dead, gone or no longer ahead of us
        if (queueLeader != null && !IsValidQueueLeader(queueLeader))
            queueLeader = null;

        if (Time.time < nextQueueDecisionTime) return;
        nextQueueDecisionTime = Time.time + enemy.stats.queueDecisionInterval * Random.Range(0.7f, 1.3f);

        queueLeader = null;
        if (enemy.stats.queueBehindChance > 0 && Random.Range(0, 100) < enemy.stats.queueBehindChance)
            queueLeader = FindQueueLeader();
    }

    bool IsValidQueueLeader(Enemy other)
    {
        if (other == null || other == enemy || !other.gameObject.activeInHierarchy) return false;

        // Only line up behind the same kind of enemy: a melee rusher queueing behind a thrower that stands still at range would just wait for nothing
        if (other.GetType() != enemy.GetType()) return false;
        if (other.health != null && other.health.IsDead) return false;

        EnemyState s = other.stateMachine.CurrentState;
        if (!(s is EnemyChaseState || s is EnemyAttackState || s is EnemySidestepState)) return false;

        Vector3 playerPos = Player.instance.transform.position;
        float myDist = Vector3.Distance(enemy.transform.position, playerPos);
        float theirDist = Vector3.Distance(other.transform.position, playerPos);

        // Must be clearly ahead of us (also rules out two enemies lining up behind each other)
        if (theirDist > myDist - 1f) return false;
        return Vector3.Distance(enemy.transform.position, other.transform.position) <= enemy.stats.queueBehindRange * 1.5f;
    }

    Enemy FindQueueLeader()
    {
        allEnemiesBuffer = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        Enemy best = null;
        float bestDist = enemy.stats.queueBehindRange;
        foreach (Enemy other in allEnemiesBuffer)
        {
            if (!IsValidQueueLeader(other)) continue;
            float d = Vector3.Distance(enemy.transform.position, other.transform.position);
            if (d < bestDist) { bestDist = d; best = other; }
        }
        return best;
    }

    Vector3 GetQueuePosition(Enemy leader, Vector3 fallback)
    {
        // "Behind" = opposite to where the leader is heading (his path), or away from the player when he is standing
        Vector3 heading = Vector3.zero;
        if (leader.agent != null && leader.agent.enabled && leader.agent.velocity.sqrMagnitude > 0.1f)
            heading = leader.agent.velocity;
        else
            heading = Player.instance.transform.position - leader.transform.position;
        heading.y = 0f;
        if (heading.sqrMagnitude < 0.0001f) return fallback;

        Vector3 pos = leader.transform.position - heading.normalized * enemy.stats.queueBehindSpacing;
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out UnityEngine.AI.NavMeshHit hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
            return hit.position;
        return fallback;
    }

    private void GenerateRandomOffset()
    {
        // Find valid NavMesh positions around the player in a smarter way
        List<Vector3> validPositions = new List<Vector3>();
        
        // Determine appropriate radius range based on attack type
        float minRadius, maxRadius, radiusStep;
        
        // Apply optimizations based on settings
        bool useOptimizations = enemy.stats.useOptimizedPathfinding;
        int maxPositionsToFind = useOptimizations ? 3 : 8; // Stop early if optimized
        
        if (enemy.stats.attackType == AttackType.Ranged)
        {
            // Ranged enemies: Try multiple ranges from ideal to acceptable
            // Start at 50% of attack range and go up to 90%
            minRadius = enemy.stats.attackRange * 0.5f;
            maxRadius = enemy.stats.attackRange * 0.9f;
            radiusStep = useOptimizations ? (maxRadius - minRadius) / 2f : (maxRadius - minRadius) / 4f; // 3 or 5 samples
        }
        else
        {
            // Melee enemies: get close and surround
            minRadius = 1.5f;
            maxRadius = 3.5f;
            radiusStep = 1f;
        }
        
        // NavMesh position of the player: candidates must be visible from here (no wall in between)
        Vector3 playerNavPos = Player.instance.transform.position;
        if (UnityEngine.AI.NavMesh.SamplePosition(playerNavPos, out UnityEngine.AI.NavMeshHit playerNavHit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            playerNavPos = playerNavHit.position;

        // Randomize starting direction to spread enemies better
        int startDirection = Random.Range(0, 8);
        
        // Try 8 directions around the player (N, NE, E, SE, S, SW, W, NW)
        for (int i = 0; i < 8; i++)
        {
            if (validPositions.Count >= maxPositionsToFind)
                break; // Early exit once we have enough valid positions
            
            int dirIndex = (startDirection + i) % 8;
            float angle = dirIndex * 45f * Mathf.Deg2Rad;
            
            // Try different distances (for ranged, prefer farther distances first)
            for (float radius = maxRadius; radius >= minRadius; radius -= radiusStep)
            {
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius
                );
                
                Vector3 targetPos = Player.instance.transform.position + offset;
                UnityEngine.AI.NavMeshHit hit;
                
                // Check if position is valid on NavMesh
                if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    // Skip points hidden behind a wall from the player (the snap above can land on the other side of one)
                    if (UnityEngine.AI.NavMesh.Raycast(playerNavPos, hit.position, out UnityEngine.AI.NavMeshHit blockHit, UnityEngine.AI.NavMesh.AllAreas))
                        continue;

                    // Check path reachability (optimized or full based on settings)
                    bool needsPathCheck;
                    if (useOptimizations)
                    {
                        // OPTIMIZATION: Skip expensive CalculatePath check most of the time
                        // Only verify reachability for critical positions or randomly
                        needsPathCheck = validPositions.Count == 0 || Random.value < 0.3f;
                    }
                    else
                    {
                        // Full quality: Always validate paths
                        needsPathCheck = true;
                    }
                    
                    if (needsPathCheck)
                    {
                        // Check if there's a clear path from enemy to this position
                        UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
                        if (!enemy.agent.CalculatePath(hit.position, path) || path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete)
                        {
                            continue; // Skip this position
                        }
                    }
                    
                    // Check if not too close to walls (skip for optimized mode to save performance)
                    bool tooCloseToWall = false;
                    if (!useOptimizations)
                    {
                        tooCloseToWall = IsPositionNearWall(hit.position, 0.8f);
                    }
                    
                    if (!tooCloseToWall)
                    {
                        validPositions.Add(hit.position);
                        
                        if (validPositions.Count >= maxPositionsToFind)
                            break; // Early exit from radius loop
                    }
                }
            }
        }
        
        // If we found valid positions, choose one
        if (validPositions.Count > 0)
        {
            // Pick a random valid position
            Vector3 chosenPosition = validPositions[Random.Range(0, validPositions.Count)];
            destinationOffset = chosenPosition - Player.instance.transform.position;
        }
        else
        {
            // Fallback: for both types, just head directly to player with small offset
            // This ensures enemies don't get stuck if no ideal position exists
            float smallRadius = Random.Range(0.5f, 1.5f);
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            destinationOffset = new Vector3(
                Mathf.Cos(randomAngle) * smallRadius,
                0f,
                Mathf.Sin(randomAngle) * smallRadius
            );
        }
    }
    
    private bool IsPositionNearWall(Vector3 position, float checkRadius)
    {
        // Check if position is too close to walls/obstacles
        Collider[] colliders = Physics.OverlapSphere(position, checkRadius, LayerMask.GetMask("Default", "Wall"));
        
        // Filter out enemy colliders
        foreach (var col in colliders)
        {
            if (col.GetComponent<Enemy>() == null && col.GetComponent<Player>() == null)
            {
                return true; // Found a wall/obstacle nearby
            }
        }
        
        return false;
    }
}
