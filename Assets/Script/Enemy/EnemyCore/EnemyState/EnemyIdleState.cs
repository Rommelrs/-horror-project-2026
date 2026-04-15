using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyIdleState : EnemyState
{
    float idleTimer = 0f;

    public EnemyIdleState(Enemy enemy, EnemyStateMachine sm) : base(enemy, sm) { }

    public override void Enter()
    {
        idleTimer = Random.Range(1f, 3f);

        if(enemy.agent.isActiveAndEnabled && enemy.agent.isOnNavMesh)
            enemy.agent.isStopped = true;

        canLeave = false;
    }

    public override void Update()
    {
        //Check if player detected
        if (!enemy.playerDetected)
        {
            bool newDetection = false;
            
            // Check detection based on mode
            if (enemy.stats.detectionMode == DetectionMode.FOVCone)
            {
                // Realistic: must be in range AND in FOV cone
                newDetection = enemy.PlayerInRange(enemy.stats.visionRadius) && enemy.PlayerInFOV();
            }
            else // SphereRadius
            {
                // Omniscient: just check range (360 degrees)
                newDetection = enemy.PlayerInRange(enemy.stats.visionRadius);
            }
            
            if(enemy.playerDetected != newDetection)
            {
                enemy.playerDetected = newDetection;
                enemy.onPlayerDetected?.Invoke();
            }
        }
        else
        {
            bool newDetection = false;
            
            // Check detection based on mode (same as above)
            if (enemy.stats.detectionMode == DetectionMode.FOVCone)
            {
                newDetection = enemy.PlayerInRange(enemy.stats.visionRadius) && enemy.PlayerInFOV();
            }
            else // SphereRadius
            {
                newDetection = enemy.PlayerInRange(enemy.stats.visionRadius);
            }
            
            if (enemy.playerDetected != newDetection)
            {
                enemy.playerDetected = newDetection;
            }
        }

        //Check State Leave Condition
        enemy.CheckLeaveCondition(this);

        //Wait for idle time
        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0) canLeave = true;
    }
}
