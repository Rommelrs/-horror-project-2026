using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class EnemyDashAttackState : EnemyState
{
    float leaveTime;
    Coroutine applyDashForceCR;
    Vector3 dashTargetPosition;

    public Action DashAnimStarted;
    public Action DashStarted;
    public Action DashEnded;

    bool damaged = false;
    CollisionDetectionMode originalCollisionMode;

    public EnemyDashAttackState(Enemy enemy, EnemyStateMachine sm) : base(enemy, sm)
    {
        this.enemy = enemy;
    }

    public override void Enter()
    {
        canLeave = false;
        damaged = false;

        leaveTime = Time.time + enemy.stats.dashAttackDuration;

        // Keep NavMeshAgent enabled but stop movement during windup
        enemy.agent.enabled = true;
        enemy.agent.isStopped = true; // Stop during windup animation
        enemy.agent.speed = enemy.stats.dashSpeed; // Sprint speed for later

        //Set Target Position past the player
        Vector3 playerPos = Player.instance.transform.position;
        Vector3 direction = playerPos - enemy.transform.position;
        direction.y = 0;
        direction.Normalize();
        
        // Target is past the player
        dashTargetPosition = playerPos + direction * enemy.stats.dashTargetOffset;

        //Face player
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            enemy.transform.rotation = targetRotation;
        }

        if (applyDashForceCR != null) enemy.StopCoroutine(applyDashForceCR);
        applyDashForceCR = enemy.StartCoroutine(Co_ApplyDashForce());
    }

    public override void Exit()
    {
        base.Exit();

        // Stop the coroutine immediately
        if (applyDashForceCR != null) enemy.StopCoroutine(applyDashForceCR);

        // Reset to normal speed
        enemy.agent.speed = enemy.stats.movementSpeed;
        enemy.agent.isStopped = false;

        // Reset ALL animation parameters - ORDER MATTERS
        // First turn off charging animation (exit Charging state)
        enemy.anim.SetBool("isCharging", false);
        // Then exit the DashAttack sub-machine completely
        enemy.anim.SetBool("DashAttack", false);

        SetDefaultEnemyLayer();

        DashEnded?.Invoke();
    }

    public override void Update()
    {
        enemy.CheckLeaveCondition(this);
        
        // canLeave is set by the coroutine when sprint finishes
    }

    IEnumerator Co_ApplyDashForce()
    {
        enemy.anim.SetBool("DashAttack", true);

        // Wait a tiny bit for the Charging_Windup animation to start
        yield return new WaitForSeconds(0.1f);
        
        // NOW spawn the weakpoint during the windup
        DashAnimStarted?.Invoke();

        // Wait for rest of windup animation (enemy stands still)
        yield return new WaitForSeconds(enemy.stats.dashAttackInitialDelay - 0.1f);

        // Now start sprinting - enable charging animation loop
        enemy.anim.SetBool("isCharging", true);
        enemy.agent.isStopped = false;
        enemy.agent.SetDestination(dashTargetPosition);

        DashStarted?.Invoke();

        // Wait minimum time for animator to transition from Windup -> Charging
        // This prevents deadlock if player is already close
        yield return new WaitForSeconds(enemy.stats.dashAnimTransitionDelay);

        // Sprint past player
        bool playedAttackAnim = false;
        bool passedPlayer = false;
        
        while (true)
        {
            yield return null;

            Vector3 currentPlayerPos = Player.instance.transform.position;
            float distanceToPlayer = Vector3.Distance(currentPlayerPos, enemy.transform.position);
            
            // Once we pass the player (get very close), don't update target anymore
            if (distanceToPlayer <= enemy.stats.attackRange * 1.5f)
            {
                if (!passedPlayer)
                {
                    // First time getting close - trigger attack animation immediately
                    enemy.anim.SetBool("isCharging", false);
                }
                passedPlayer = true;
            }
            
            // Only track player until we've reached them
            if (!passedPlayer)
            {
                // Update target position to stay aimed at player
                Vector3 directionToPlayer = currentPlayerPos - enemy.transform.position;
                directionToPlayer.y = 0;
                directionToPlayer.Normalize();
                dashTargetPosition = currentPlayerPos + directionToPlayer * enemy.stats.dashTargetOffset;
                
                // Update NavMesh destination
                if (enemy.agent.isOnNavMesh)
                {
                    enemy.agent.SetDestination(dashTargetPosition);
                }
            }

            //Attempt to Damage Player when close
            AttemptToDamage();

            // Exit conditions:
            // 1. Passed player and reached far enough away
            // 2. Time limit exceeded
            float distance = Vector3.Distance(dashTargetPosition, enemy.transform.position);
            bool agentStopped = enemy.agent.velocity.sqrMagnitude < 0.1f && !enemy.agent.pathPending;
            
            if ((passedPlayer && distance <= enemy.stats.attackRange) || Time.time > leaveTime || (passedPlayer && agentStopped))
            {
                //Stop sprint and charging animation
                enemy.agent.isStopped = true;
                enemy.anim.SetBool("isCharging", false);
                
                // Allow state to leave
                canLeave = true;

                DashEnded?.Invoke();

                yield break;
            }
        }
    }

    void AttemptToDamage()
    {
        if (damaged) return;

        if (GameManager.IsPaused) return;

        if (enemy.health.IsDead) return;

        if (Player.instance.isRolling)
            return;

        if (enemy.PlayerInRange(enemy.stats.attackRange))
        {
            damaged = true;

            //Successful hit
            Player.instance.health.Damage(enemy.stats.damage);
            
            // Play stab sound effect (2D)
            enemy.Play2DSoundEffect(enemy.stats.stabSFX);
        }
    }

    public void SetDefaultEnemyLayer()
    {
        enemy.transform.gameObject.layer = 7;
    }

    public void SetNoCollisionEnemyLayer()
    {
        enemy.transform.gameObject.layer = 11;
    }
}
