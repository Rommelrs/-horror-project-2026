using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class EnemySidestepState : EnemyState
{
    bool isSidesteppingToRight = false;
    Quaternion targetRotation;
    float leaveTime;
    float earlyAttackCheckTime;
    Coroutine applySidestepCR;

    public float currentSidestepDuration = 0;

    public EnemySidestepState(Enemy enemy, EnemyStateMachine sm) : base(enemy, sm)
    {
        this.enemy = enemy;
    }

    public override void Enter()
    {
        enemy.agent.enabled = false;
        enemy.rb.useGravity = true;
        enemy.rb.isKinematic = false;

        isSidesteppingToRight = UnityEngine.Random.Range(0, 2) == 0;

        //Set Sidestep Duration
        currentSidestepDuration = UnityEngine.Random.Range(enemy.stats.sideStepDurationMin, enemy.stats.sideStepDurationMax);

        leaveTime = Time.time + currentSidestepDuration;
        earlyAttackCheckTime = Time.time + 1f; // After 1 second, can exit early to attack if close
        canLeave = false;

        enemy.anim.SetBool("Strafing", true);

        if (enemy is RunnerEnemy runnerEnemy)
        {
            runnerEnemy.sideStepToRight = !runnerEnemy.sideStepToRight;
            isSidesteppingToRight = runnerEnemy.sideStepToRight;
            enemy.anim.SetFloat("StrafingIndex", isSidesteppingToRight ? 1 : 0);
        }
        else
        {
            enemy.anim.SetFloat("StrafingIndex", isSidesteppingToRight ? 1 : 0);
        }

        if (applySidestepCR != null) enemy.StopCoroutine(applySidestepCR);
        applySidestepCR = enemy.StartCoroutine(Co_ApplySidestep());
    }

    public override void Exit()
    {
        base.Exit();

        enemy.rb.velocity = Vector3.zero;
        enemy.rb.angularVelocity = Vector3.zero;
        enemy.rb.useGravity = false;
        enemy.rb.isKinematic = true;

        enemy.agent.enabled = true;
        
        // Immediately set destination to resume chase without pause
        if (enemy.agent.isOnNavMesh && Player.instance != null)
        {
            enemy.agent.SetDestination(Player.instance.transform.position);
            enemy.agent.isStopped = false;
        }
        else
        {
            enemy.agent.Warp(enemy.transform.position);
        }

        enemy.anim.SetBool("Strafing", false);

        if (applySidestepCR != null) enemy.StopCoroutine(applySidestepCR);
    }

    public override void Update()
    {
        enemy.CheckLeaveCondition(this);
        
        // Normal time-based exit
        canLeave = Time.time > leaveTime;
        
        // Early exit if player is within attack range
        if (Time.time > earlyAttackCheckTime && enemy.PlayerInRange(enemy.stats.attackRange))
        {
            canLeave = true;
        }

        //Look at player (flatten Y to prevent vertical tilt)
        Vector3 lookDirection = Player.instance.transform.position - enemy.transform.position;
        lookDirection.y = 0f; // Keep rotation horizontal only
        targetRotation = Quaternion.LookRotation(lookDirection);
        enemy.transform.rotation = Quaternion.Lerp(enemy.transform.rotation, targetRotation, enemy.stats.sideStepLookRotation * Time.deltaTime);
    }


    IEnumerator Co_ApplySidestep()
    {
        while (true)
        {
            yield return new WaitForFixedUpdate();

            if (isSidesteppingToRight)
            {
                //Sidestepping to Right
                if (enemy.rb.velocity.magnitude < enemy.stats.sideStepSpeed)
                    enemy.rb.AddForce(enemy.transform.right * enemy.stats.sideStepAccelaration * Time.fixedDeltaTime, ForceMode.Impulse);
                enemy.rb.velocity = Vector3.ClampMagnitude(enemy.rb.velocity, enemy.stats.sideStepSpeed);
            }
            else
            {
                //Sidestepping to Left
                if (enemy.rb.velocity.magnitude < enemy.stats.sideStepSpeed)
                    enemy.rb.AddForce(-enemy.transform.right * enemy.stats.sideStepAccelaration * Time.fixedDeltaTime, ForceMode.Impulse);
                enemy.rb.velocity = Vector3.ClampMagnitude(enemy.rb.velocity, enemy.stats.sideStepSpeed);
            }
        }
    }
}
