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
    float savedAgentSpeed = 0f;

    public EnemyKnockBackState(Enemy enemy, EnemyStateMachine sm) : base(enemy, sm) 
    {
        this.enemy = enemy;
    }

    public override void Enter()
    {
        canLeave = false;
        stateChangeTrigerred = false;
        
        Debug.Log("KnockbackState.Enter: " + enemy.name + ", isDamageByWeakpointHit = " + enemy.health.isDamageByWeakpointHit);
        Debug.Log("KnockbackState.Enter: canHeavyKnockBack = " + enemy.stats.canHeavyKnockBack);

        if (enemy.stats.canHeavyKnockBack && enemy.health.isDamageByWeakpointHit)
        {
            //Heavy Knockback
            heavyKnockback = true;
            Debug.Log("KnockbackState.Enter: HEAVY KNOCKBACK for " + enemy.name);

            //Trigger knockback Animation
            enemy.anim.SetBool("HeavyKnockback", true);

            knockbackTime = Time.time + enemy.stats.heavyKnockBackDuration;
        }
        else
        {
            //Light Knockback
            heavyKnockback = false;
            Debug.Log("KnockbackState.Enter: LIGHT KNOCKBACK for " + enemy.name);

            //Trigger knockback Animation
            enemy.anim.SetTrigger("Knockback");

            knockbackTime = Time.time + enemy.stats.maxKnockbackTime;
        }

        //Look at player
        Quaternion targetRotation = Quaternion.LookRotation(Player.instance.transform.position - enemy.transform.position);
        enemy.transform.rotation = targetRotation;
        
        Debug.Log("KnockbackState.Enter: About to call KnockBack with force multiplier = " + enemy.damageForceMultiplier);
        KnockBack(enemy.damageDirection, enemy.damageForceMultiplier);
    }

    public override void Exit()
    {
        base.Exit();

        if (knockBackCR != null) enemy.StopCoroutine(knockBackCR);

        enemy.rb.linearVelocity = Vector3.zero;
        enemy.rb.angularVelocity = Vector3.zero;
        enemy.rb.useGravity = false;
        enemy.rb.isKinematic = true;

        enemy.agent.enabled = true;
        enemy.agent.Warp(enemy.transform.position);

        //Reset Heavy Knockback
        enemy.anim.SetBool("HeavyKnockback", false);
        
        // Reset the weakpoint flag after knockback is complete
        enemy.health.isDamageByWeakpointHit = false;
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
                    // Fixed enemies go back to idle, others chase
                    EnemyState nextState = enemy.stats.enemyType == EnemyType.Fixed ? enemy.idleState : enemy.chaseState;
                    enemy.StartCoroutine(Co_WakeUpThenChangeState(nextState));
                }
            }
            else
            {
                if (!stateChangeTrigerred)
                {
                    stateChangeTrigerred = true;
                    // Fixed enemies go back to idle, others chase
                    EnemyState nextState = enemy.stats.enemyType == EnemyType.Fixed ? enemy.idleState : enemy.chaseState;
                    stateMachine.ChangeState(nextState);
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
        
        Debug.Log("Co_KnockBack: Starting for " + enemy.name);
        Debug.Log("Co_KnockBack: Agent enabled = " + enemy.agent.enabled + ", isOnNavMesh = " + enemy.agent.isOnNavMesh);
        
        // Save current agent speed before disabling (important for dynamic chase speed)
        if (enemy.agent.enabled && enemy.agent.isOnNavMesh)
        {
            savedAgentSpeed = enemy.agent.speed;
            Debug.Log("Co_KnockBack: Saved agent speed = " + savedAgentSpeed);
        }
        
        enemy.agent.enabled = false;
        Debug.Log("Co_KnockBack: Agent disabled");
        
        enemy.rb.useGravity = true;
        enemy.rb.isKinematic = false;
        
        float calculatedForce = enemy.stats.knockBackForce * forceMultiplier;
        Debug.Log("Co_KnockBack: Applying force = " + calculatedForce + " (knockBackForce=" + enemy.stats.knockBackForce + ", multiplier=" + forceMultiplier + ")");
        Debug.Log("Co_KnockBack: Direction = " + direction.normalized);
        
        enemy.rb.AddForce(direction.normalized * calculatedForce, ForceMode.Impulse);

        yield return new WaitForFixedUpdate();
      
        yield return new WaitForSeconds(0.2f);
        yield return new WaitUntil(() => enemy.rb.linearVelocity.magnitude < enemy.stats.stillThreshold);

        enemy.rb.linearVelocity = Vector3.zero;
        enemy.rb.angularVelocity = Vector3.zero;
        enemy.rb.useGravity = false;
        enemy.rb.isKinematic = true;

        enemy.agent.enabled = true;
        enemy.agent.Warp(enemy.transform.position);
        
        // Restore saved agent speed (important for dynamic chase speed)
        if (savedAgentSpeed > 0 && enemy.agent.isOnNavMesh)
        {
            enemy.agent.speed = savedAgentSpeed;
        }

        // Keep agent stopped during heavy knockback (wakeup animation will play)
        if (heavyKnockback && enemy.agent.isOnNavMesh)
            enemy.agent.isStopped = true;

        if(!heavyKnockback)
            canLeave = true;
    }
}
