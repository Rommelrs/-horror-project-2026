using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ToolBox.Pools;

public class EnemyDeathState : EnemyKnockBackState
{
    public EnemyDeathState(Enemy enemy, EnemyStateMachine sm) : base(enemy, sm) { }

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

        // Stop knockback layer
        enemy.anim.ResetTrigger("Knockback");
        enemy.anim.SetBool("HeavyKnockback", false);
        if (enemy.anim.layerCount > 1)
            enemy.anim.SetLayerWeight(1, 0);
        
        enemy.anim.SetBool("Dead", true);

        if(enemy.stats.disableEnemyAfterDeath)
            enemy.StartCoroutine(Co_ReleaseAfterTime());

        knockbackTime = Time.time;
        KnockBack(enemy.damageDirection, enemy.damageForceMultiplier);
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
