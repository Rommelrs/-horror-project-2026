using System.Collections;
using UnityEngine;
using ToolBox.Pools;

public class RunnerEnemyDeathState : EnemyDeathState
{
    public RunnerEnemyDeathState(Enemy enemy, EnemyStateMachine sm) : base(enemy, sm) { }

    public override void Enter()
    {
        enemy.deathCollider.enabled = true;

        if (enemy.agent.isActiveAndEnabled)
            enemy.agent.isStopped = true;

        //Choose Different Death Animation
        if (enemy.health.isDamageByWeakpointHit)
        {
            enemy.anim.SetInteger("DeadIndex", 1);

            //Look at player
            Quaternion targetRotation = Quaternion.LookRotation(Player.instance.transform.position - enemy.transform.position);
            enemy.transform.rotation = targetRotation;
        }
        else
            enemy.anim.SetInteger("DeadIndex", 0);

        // Stop knockback layer completely
        enemy.anim.ResetTrigger("Knockback");
        enemy.anim.SetBool("HeavyKnockback", false);
        if (enemy.anim.layerCount > 1)
            enemy.anim.SetLayerWeight(1, 0);
        
        // Reset all Runner-specific parameters
        enemy.anim.SetFloat("Speed", 0f);
        enemy.anim.SetFloat("Velocity", 0f);
        enemy.anim.SetBool("Attack", false);
        enemy.anim.SetBool("DashAttack", false);
        enemy.anim.SetBool("isCharging", false);
        enemy.anim.SetBool("Strafing", false);
        
        enemy.anim.SetBool("Dead", true);

        if(enemy.stats.disableEnemyAfterDeath)
            enemy.StartCoroutine(Co_ReleaseAfterTime());
        
        // RUNNER-SPECIFIC WORKAROUND: Force death animation
        enemy.StartCoroutine(ForceRunnerDeathAnimation());
    }
    
    void ApplyDeathPhysics()
    {
        // Keep NavMeshAgent active to prevent animator from freezing
        if (enemy.agent.isActiveAndEnabled)
        {
            enemy.agent.velocity = enemy.damageDirection.normalized * enemy.stats.knockBackForce * enemy.damageForceMultiplier;
        }
    }
    
    System.Collections.IEnumerator ForceRunnerDeathAnimation()
    {
        // Wait one frame for all parameter changes to take effect
        yield return null;
        
        // WORKAROUND for Runner's broken Die animation:
        // The Die animation clip has no keyframe data, so we need to force it
        
        // Rebind animator to reset state
        enemy.anim.Rebind();
        
        // CRITICAL: Turn OFF Dead parameter so Any State → Die doesn't override
        enemy.anim.SetBool("Dead", false);
        
        // Play death animation directly
        enemy.anim.Play("Die", 0, 0f);
        
        // Wait for animation to start, then apply physics
        yield return new WaitForSeconds(0.1f);
        ApplyDeathPhysics();
    }
    
    IEnumerator Co_ReleaseAfterTime()
    {
        yield return new WaitForSeconds(enemy.stats.releaseTime);

        float time = 1f;
        while (time > 0)
        {
            time -= Time.deltaTime * 2f;
            if (time <= float.Epsilon) time = 0f;

            yield return null;
        }

        yield return null;
        this.enemy.gameObject.Release();
    }

    public override void Update()
    {
        // Override parent Update() to prevent state changes during death
    }
}
