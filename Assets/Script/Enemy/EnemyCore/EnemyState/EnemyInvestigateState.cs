using UnityEngine;

public class EnemyInvestigateState : EnemyState
{
    private Vector3 targetPosition;
    private float investigateTimer;
    private bool reachedTarget;
    private float stoppingDistance = 1f;
    private float travelTimeout = 10f; // Max time to reach target
    private float travelTimer;

    public EnemyInvestigateState(Enemy enemy, EnemyStateMachine sm) : base(enemy, sm) { }

    public void SetTargetPosition(Vector3 position)
    {
        // Add random offset so enemies spread out
        Vector2 randomOffset = Random.insideUnitCircle * 3f;
        targetPosition = position + new Vector3(randomOffset.x, 0, randomOffset.y);
    }

    public override void Enter()
    {
        reachedTarget = false;
        investigateTimer = enemy.stats.investigateDuration;
        travelTimer = travelTimeout;
        
        enemy.agent.isStopped = false;
        
        if (!enemy.agent.enabled)
            enemy.agent.enabled = true;
            
        if (!enemy.agent.isOnNavMesh)
            enemy.agent.Warp(enemy.transform.position);
        
        // Move towards target
        enemy.agent.speed = enemy.stats.movementSpeed;
        enemy.agent.SetDestination(targetPosition);
    }

    public override void Update()
    {
        // Check if player detected during investigation
        if (EnemyDetectionHelper.CheckDetection(enemy))
        {
            enemy.playerDetected = true;
            enemy.stateMachine.ChangeState(enemy.chaseState);
            return;
        }

        if (!reachedTarget)
        {
            // Check if reached target
            float distance = Vector3.Distance(enemy.transform.position, targetPosition);
            if (distance <= stoppingDistance + enemy.agent.stoppingDistance)
            {
                reachedTarget = true;
                enemy.agent.isStopped = true;
            }
            
            // Timeout - can't reach target, give up
            travelTimer -= Time.deltaTime;
            if (travelTimer <= 0 || (enemy.agent.hasPath && enemy.agent.remainingDistance < 0.1f && enemy.agent.velocity.sqrMagnitude < 0.01f))
            {
                reachedTarget = true;
                enemy.agent.isStopped = true;
            }
        }
        else
        {
            // Idle at location
            investigateTimer -= Time.deltaTime;
            
            if (investigateTimer <= 0)
            {
                // Return to wandering or idle
                if (enemy.stats.enemyType == EnemyType.Wandering)
                {
                    enemy.stateMachine.ChangeState(enemy.enemyWanderState);
                }
                else
                {
                    enemy.stateMachine.ChangeState(enemy.idleState);
                }
            }
        }
    }

    public override void Exit()
    {
        reachedTarget = false;
    }
}
