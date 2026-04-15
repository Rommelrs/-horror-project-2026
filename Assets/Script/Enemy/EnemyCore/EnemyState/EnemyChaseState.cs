using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyChaseState : EnemyState
{
    float playerOutOfVisionDuration = 0f;
    float initializeLookDuration = 2f;

    float lookEndTime;
    Quaternion targetRotation;
    
    // Random offset to make enemies spread out instead of all taking same path
    private Vector3 destinationOffset;
    private float offsetRecalculateTime = 0f;
    private float offsetRecalculateInterval = 3f; // Recalculate offset every 3 seconds

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

        lookEndTime = Time.time + initializeLookDuration;
        
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

        if (Player.instance == null)
            return;

        if (Time.time < lookEndTime && enemy.chaseStartDelayEnabled)
        {
            if (!enemy.agent.isStopped)
                enemy.agent.isStopped = true;

            //Look at player
            targetRotation = Quaternion.LookRotation(Player.instance.transform.position - enemy.transform.position);
            enemy.transform.rotation = Quaternion.Lerp(enemy.transform.rotation, targetRotation, enemy.stats.rotationSmoothness * Time.deltaTime);
        }
        else
        {
            canLeave = true;
            enemy.chaseStartDelayEnabled = false;

            //Chase Player
            if (enemy.agent.isOnNavMesh)
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
                
                // Advanced pathfinding: recalculate offset periodically
                if (enemy.stats.useAdvancedPathfinding)
                {
                    // If already very close to player, ignore offset and go direct
                    if (distanceToPlayer <= enemy.stats.attackRange * 1.2f)
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
                
                enemy.agent.SetDestination(targetPosition);

                if(enemy.agent.isStopped)
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
