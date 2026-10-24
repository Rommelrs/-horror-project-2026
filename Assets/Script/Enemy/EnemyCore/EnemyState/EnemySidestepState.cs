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
    float speedFactor = 1f;

    // Sidewalk mode (Stats.sideStepAnimGroundSpeed > 0): smooth velocity + strafe animation speed matched to the ground speed
    static readonly int strafeAnimSpeedHash = Animator.StringToHash("StrafeAnimSpeed");
    bool sidewalkMode;
    float sidewalkTargetSpeed;
    float sidewalkSpeed;

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

        // Slower or faster step each time
        speedFactor = 1f + UnityEngine.Random.Range(-enemy.stats.sideStepSpeedVariation, enemy.stats.sideStepSpeedVariation);

        // (the Runner takes part too: his strafe clips cover only ~0.2 m/s of ground at normal speed, so at 1.8 m/s he skated;
        //  the Sidestep state of RunnerEnemyController has a StrafeAnimSpeed speed parameter now)
        sidewalkMode = enemy.stats.sideStepAnimGroundSpeed > 0f;
        sidewalkSpeed = 0f;
        if (sidewalkMode)
        {
            float cap = enemy.stats.sideStepAnimGroundSpeed * enemy.stats.sideStepMaxAnimSpeed;
            sidewalkTargetSpeed = Mathf.Clamp(enemy.stats.sideStepSpeed * speedFactor, 0.05f, cap);
            enemy.anim.SetFloat(strafeAnimSpeedHash, enemy.stats.sideStepMinAnimSpeed);
        }

        leaveTime = Time.time + currentSidestepDuration;
        // After 1 second, can exit early to attack if close (aim-reaction sidesteps are short, so check sooner)
        earlyAttackCheckTime = Time.time + (enemy.stats.sideStepOnlyWhenAimedAt ? Mathf.Min(1f, currentSidestepDuration * 0.5f) : 1f);
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

        enemy.rb.linearVelocity = Vector3.zero;
        enemy.rb.angularVelocity = Vector3.zero;
        enemy.rb.useGravity = false;
        enemy.rb.isKinematic = true;

        // Give the strafe state back its normal playback speed (the parameter is only meaningful while sidewalking)
        enemy.anim.SetFloat(strafeAnimSpeedHash, 1f);

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
        if (Time.time > earlyAttackCheckTime && enemy.PlayerInAttackRange())
        {
            canLeave = true;
        }

        // Sidewalk: the feet follow what the body actually does (also slows the legs when he is pushing against something)
        if (sidewalkMode)
        {
            Vector3 v = enemy.rb.linearVelocity;
            float actual = new Vector2(v.x, v.z).magnitude;
            float animSpeed = Mathf.Clamp(actual / enemy.stats.sideStepAnimGroundSpeed, enemy.stats.sideStepMinAnimSpeed, enemy.stats.sideStepMaxAnimSpeed);
            enemy.anim.SetFloat(strafeAnimSpeedHash, animSpeed);
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

            if (sidewalkMode)
            {
                // Smooth ramp to the chosen walking speed, straight sideways, gravity untouched
                sidewalkSpeed = Mathf.MoveTowards(sidewalkSpeed, sidewalkTargetSpeed, enemy.stats.sideStepAccelaration * Time.fixedDeltaTime);
                Vector3 side = enemy.transform.right * (isSidesteppingToRight ? 1f : -1f);
                side.y = 0f;
                side.Normalize();
                enemy.rb.linearVelocity = side * sidewalkSpeed + Vector3.up * enemy.rb.linearVelocity.y;
                continue;
            }

            // The old version pushed with a small impulse each physics step (accel * dt = 0.12 m/s per step for the Runner), which is
            // about what the floor's friction takes away again (0.6 * 9.81 m/s^2 for a 1 kg body): he strafed in place at ~0.2-0.5 m/s
            // instead of the configured 1.8 m/s. Drive the sideways velocity directly, like the sidewalk mode does (gravity untouched).
            float maxSpeed = enemy.stats.sideStepSpeed * speedFactor;
            sidewalkSpeed = Mathf.MoveTowards(sidewalkSpeed, maxSpeed, enemy.stats.sideStepAccelaration * Time.fixedDeltaTime);
            Vector3 dirSide = enemy.transform.right * (isSidesteppingToRight ? 1f : -1f);
            dirSide.y = 0f;
            dirSide.Normalize();
            enemy.rb.linearVelocity = dirSide * sidewalkSpeed + Vector3.up * enemy.rb.linearVelocity.y;
        }
    }
}
