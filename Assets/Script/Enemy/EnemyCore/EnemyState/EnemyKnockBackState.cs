using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class EnemyKnockBackState : EnemyState
{
    Coroutine knockBackCR;
    protected float knockbackTime;

    bool heavyKnockback = false;
    bool stateChangeTrigerred = false;

    public EnemyKnockBackState(Enemy enemy, EnemyStateMachine sm) : base(enemy, sm) 
    {
        this.enemy = enemy;
    }

    public override void Enter()
    {
        canLeave = false;
        stateChangeTrigerred = false;

        if (enemy.stats.canHeavyKnockBack && enemy.health.isDamageByWeakpointHit)
        {
            //Heavy Knockback
            heavyKnockback = true;

            //Trigger knockback Animation
            enemy.anim.SetBool("HeavyKnockback", true);

            knockbackTime = Time.time + enemy.stats.heavyKnockBackDuration;
        }
        else
        {
            //Light Knockback
            heavyKnockback = false;

            //Trigger knockback Animation
            enemy.anim.SetTrigger("Knockback");

            knockbackTime = Time.time + enemy.stats.maxKnockbackTime;
        }

        //Look at player
        Quaternion targetRotation = Quaternion.LookRotation(Player.instance.transform.position - enemy.transform.position);
        enemy.transform.rotation = targetRotation;

        KnockBack(enemy.damageDirection, enemy.damageForceMultiplier);
    }

    public override void Exit()
    {
        base.Exit();

        if (knockBackCR != null) enemy.StopCoroutine(knockBackCR);

        enemy.rb.velocity = Vector3.zero;
        enemy.rb.angularVelocity = Vector3.zero;
        enemy.rb.useGravity = false;
        enemy.rb.isKinematic = true;

        enemy.agent.enabled = true;
        enemy.agent.Warp(enemy.transform.position);

        //Reset Heavy Knockback
        enemy.anim.SetBool("HeavyKnockback", false);
    }

    public override void Update()
    {
        if (canLeave || Time.time > knockbackTime)
        {
            if (heavyKnockback)
            {
                if (!stateChangeTrigerred)
                {
                    stateChangeTrigerred = true;
                    enemy.StartCoroutine(Co_WakeUpThenChangeState(enemy.chaseState));
                }
            }
            else
            {
                if (!stateChangeTrigerred)
                {
                    stateChangeTrigerred = true;
                    stateMachine.ChangeState(enemy.chaseState);
                }
            }
          
        }
    }

    IEnumerator Co_WakeUpThenChangeState(EnemyState enemyState)
    {
        enemy.anim.SetBool("HeavyKnockback", false);

        // Wait for the wakeup animation to finish
        yield return new WaitForSeconds(enemy.stats.wakeUpDuration);

        stateMachine.ChangeState(enemyState);
    }

    protected void KnockBack(Vector3 direction, float forceMultiplier)
    {
        if (knockBackCR != null) enemy.StopCoroutine(knockBackCR);
        knockBackCR = enemy.StartCoroutine(Co_KnockBack(direction, forceMultiplier));
    }

    IEnumerator Co_KnockBack(Vector3 direction, float forceMultiplier = 1f)
    {
        yield return null;
        enemy.agent.enabled = false;
        enemy.rb.useGravity = true;
        enemy.rb.isKinematic = false;
        enemy.rb.AddForce(direction.normalized * enemy.stats.knockBackForce * forceMultiplier, ForceMode.Impulse);

        yield return new WaitForFixedUpdate();
      
        yield return new WaitForSeconds(0.2f);
        yield return new WaitUntil(() => enemy.rb.velocity.magnitude < enemy.stats.stillThreshold);

        enemy.rb.velocity = Vector3.zero;
        enemy.rb.angularVelocity = Vector3.zero;
        enemy.rb.useGravity = false;
        enemy.rb.isKinematic = true;

        enemy.agent.enabled = true;
        enemy.agent.Warp(enemy.transform.position);

        // Keep agent stopped during heavy knockback (wakeup animation will play)
        if (heavyKnockback && enemy.agent.isOnNavMesh)
            enemy.agent.isStopped = true;

        if(!heavyKnockback)
            canLeave = true;
    }
}
