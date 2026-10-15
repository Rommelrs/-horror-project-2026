using System.Collections;
using System.Collections.Generic;
using ToolBox.Pools;
using UnityEngine;
using UnityEngine.AI;

public class HeadshotEnemy : Enemy
{
    public EnemySidestepState enemySidestepState;

    Coroutine sideStepCheckCR;
    float lastSideStepTime;

    public override void Awake()
    {
        base.Awake();

        enemySidestepState = new EnemySidestepState(this, stateMachine);
    }

    public override void Start()
    {
        base.Start();

        if (sideStepCheckCR != null) StopCoroutine(sideStepCheckCR);
        sideStepCheckCR = StartCoroutine(Co_SidestepCheck());
    }

    public override void OnPool()
    {
        base.OnPool();

        if (sideStepCheckCR != null) StopCoroutine(sideStepCheckCR);
        sideStepCheckCR = StartCoroutine(Co_SidestepCheck());
    }

    const float aimCheckInterval = 0.1f;

    IEnumerator Co_SidestepCheck()
    {
        float aimedTime = 0f;
        float nextAimRollTime = 0f;

        while (true)
        {
            if (stats.sideStepOnlyWhenAimedAt)
            {
                // React to the player aiming at us instead of strafing at random
                yield return new WaitForSeconds(aimCheckInterval);

                if (Player.instance == null || health.IsDead) continue;

                aimedTime = IsPlayerAimingAtMe() ? aimedTime + aimCheckInterval : 0f;

                if (aimedTime >= stats.sideStepReactionTime
                    && Time.time > lastSideStepTime
                    && Time.time >= nextAimRollTime
                    && stateMachine.CurrentState is EnemyChaseState chase && chase.canLeave
                    && !PlayerInAttackRange()
                    && HasLineOfSightToPlayer()
                    && PlayerInRange(stats.visionRadius))
                {
                    if (Random.Range(0, 100) < stats.sideStepChance)
                    {
                        stateMachine.ChangeState(enemySidestepState);
                        lastSideStepTime = Time.time + enemySidestepState.currentSidestepDuration + stats.sideStepCooldown;
                        aimedTime = 0f;
                    }
                    else
                    {
                        nextAimRollTime = Time.time + stats.sideStepRerollInterval;
                    }
                }
                continue;
            }

            yield return new WaitForSeconds(1f);

            //Check if ready to sidestep
            if (Time.time > lastSideStepTime)
            {
                //bool playerIsLookingAtEnemy = IsPlayerLookingAtEnemy(Player.instance.transform, this.transform, 40);

                //Check if player is inside enemy vision area but NOT in attack range
                // Only strafe when the player is actually in view: sidestepping with a wall in between just slides the enemy along it
                if (Vector3.Distance(transform.position, Player.instance.transform.position) < stats.visionRadius
                    && !PlayerInAttackRange()
                    && HasLineOfSightToPlayer())
                {
                    int rand = Random.Range(0, 100);

                    //Set Random chance to start sidestep & also check if in chase state
                    if (stateMachine.CurrentState is EnemyChaseState && rand < stats.sideStepChance)
                    {
                        //Set Sidestep                     
                        stateMachine.ChangeState(enemySidestepState);

                        //Set Next sidestep ready time
                        lastSideStepTime = Time.time + enemySidestepState.currentSidestepDuration + stats.sideStepCooldown;
                    }
                }
            }
        }
    }

    public override void AlertToSound(Vector3 soundPosition)
    {
        // A sidestep (agent off, physics-driven) is a reaction to being aimed at; a gunshot must not turn it into an Investigate walk
        if (stateMachine.CurrentState == enemySidestepState)
            return;

        base.AlertToSound(soundPosition);
    }

    /// <summary>True while the player is in aim mode and the crosshair (camera forward) is pointing at this enemy's torso.</summary>
    bool IsPlayerAimingAtMe()
    {
        var weapon = Player.instance.playerWeaponSystem;
        if (weapon == null || !weapon.isAiming) return false;

        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 toMe = (transform.position + Vector3.up * 1.2f) - cam.transform.position;
        return Vector3.Angle(cam.transform.forward, toMe) <= stats.sideStepAimAngle;
    }

    public override void CheckLeaveCondition(EnemyState currentState)
    {
        base.CheckLeaveCondition(currentState);

        //Sidestep State
        if (currentState == enemySidestepState)
        {
            if (currentState.canLeave)
            {
                if (PlayerInAttackRange())
                {
                    stateMachine.ChangeState(attackState);
                    return;
                }

                if (!PlayerInAttackRange())
                {
                    stateMachine.ChangeState(chaseState);
                    return;
                }
            }
        }
    }

    public override void ResetEnemy()
    {
        base.ResetEnemy();

        //Set Next sidestep ready time
        lastSideStepTime = Time.time + enemySidestepState.currentSidestepDuration + stats.sideStepCooldown;
    }
}
