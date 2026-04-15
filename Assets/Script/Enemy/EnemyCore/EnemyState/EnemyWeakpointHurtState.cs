using System.Collections;
using UnityEngine;

public class EnemyWeakpointHurtState : EnemyState
{
    bool isFirstHit; // true = standing hurt, false = falling down hurt
    bool hasPlayedWakeup = false;
    
    public EnemyWeakpointHurtState(Enemy enemy, EnemyStateMachine sm) : base(enemy, sm)
    {
        this.enemy = enemy;
    }

    public override void Enter()
    {
        canLeave = false;
        hasPlayedWakeup = false;
        
        // Determine if this is first hit (standing) or second hit (falling)
        RunnerEnemy runner = enemy as RunnerEnemy;
        if (runner != null)
        {
            isFirstHit = (runner.weakpointHitCount == 1);
        }
        
        // Stop movement
        if (enemy.agent.isActiveAndEnabled && enemy.agent.isOnNavMesh)
            enemy.agent.isStopped = true;
        
        // Look at player
        Vector3 playerPos = Player.instance.transform.position;
        playerPos.y = enemy.transform.position.y;
        Vector3 directionToPlayer = playerPos - enemy.transform.position;
        if (directionToPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            enemy.transform.rotation = targetRotation;
        }
        
        // Play appropriate hurt animation based on which hit this is
        if (isFirstHit)
        {
            enemy.anim.Play("Weakpoint_hurt", 0, 0f);
        }
        else
        {
            enemy.anim.Play("Weakpoint_hurt_falling_down", 0, 0f);
        }
        
        // Start monitoring the animation
        enemy.StartCoroutine(MonitorAnimation());
    }

    public override void Exit()
    {
        base.Exit();
        
        if (enemy.agent.isActiveAndEnabled && enemy.agent.isOnNavMesh)
            enemy.agent.isStopped = false;
    }

    public override void Update()
    {
        // Check if player is still in range
        enemy.CheckLeaveCondition(this);
    }
    
    IEnumerator MonitorAnimation()
    {
        // Wait for hurt animation to complete
        yield return new WaitForSeconds(0.1f);
        
        while (true)
        {
            AnimatorStateInfo stateInfo = enemy.anim.GetCurrentAnimatorStateInfo(0);
            
            // Check if hurt animation has finished
            if (stateInfo.normalizedTime >= 0.95f)
            {
                // Check if enemy is dead
                if (enemy.health.IsDead)
                {
                    // Don't play wakeup, stay on death pose
                    yield break;
                }
                
                // Enemy is alive, play wakeup animation
                if (!hasPlayedWakeup)
                {
                    hasPlayedWakeup = true;
                    
                    if (isFirstHit)
                    {
                        enemy.anim.Play("Wakeup_from_weakpoint_hurt", 0, 0f);
                    }
                    else
                    {
                        enemy.anim.Play("Wakeup_from_weakpoint_hurt_falling_down", 0, 0f);
                    }
                    
                    // Wait for wakeup to complete
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }
                else
                {
                    // Wakeup animation finished
                    canLeave = true;
                    yield break;
                }
            }
            
            yield return null;
        }
    }
}
