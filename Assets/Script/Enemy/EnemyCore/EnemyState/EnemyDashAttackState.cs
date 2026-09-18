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
    /// <summary>Set true to suppress damage for this dash (cinematic push). Future dashes are unaffected since Enter() resets damaged.</summary>
    public bool disableDashDamage = false;
    /// <summary>Set true to skip the windup animation and go straight to charging/running. Auto-resets each Enter() so only the next dash is affected.</summary>
    public bool skipWindup = false;

    public EnemyDashAttackState(Enemy enemy, EnemyStateMachine sm) : base(enemy, sm)
    {
        this.enemy = enemy;
    }

    public override void Enter()
    {
        canLeave = false;
        damaged = false;

        leaveTime = Time.time + enemy.stats.dashAttackDuration;

        // Ensure agent is enabled and in a clean state
        enemy.agent.enabled = true;
        if (enemy.agent.isOnNavMesh)
        enemy.agent.ResetPath();
        else
            enemy.agent.Warp(enemy.transform.position);
        enemy.agent.isStopped = true;
        enemy.agent.speed = enemy.stats.dashSpeed;
        enemy.agent.stoppingDistance = 0f; // Never brake early during dash

        // Set target position past the player, clamped to NavMesh so SetDestination always succeeds
        Vector3 playerPos = Player.instance.transform.position;
        Vector3 direction = playerPos - enemy.transform.position;
        direction.y = 0;
        direction.Normalize();

        Vector3 idealTarget = playerPos + direction * enemy.stats.dashTargetOffset;

        UnityEngine.AI.NavMeshHit hit;
        bool targetOnNavMesh = UnityEngine.AI.NavMesh.SamplePosition(idealTarget, out hit, 3f, UnityEngine.AI.NavMesh.AllAreas);
        dashTargetPosition = targetOnNavMesh ? hit.position : playerPos;

        //Face player
        if (direction.sqrMagnitude > 0.01f)
            enemy.transform.rotation = Quaternion.LookRotation(direction);

        bool useSkipWindup = skipWindup;
        skipWindup = false;

        if (applyDashForceCR != null) enemy.StopCoroutine(applyDashForceCR);
        applyDashForceCR = enemy.StartCoroutine(Co_ApplyDashForce(useSkipWindup));
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

    IEnumerator Co_ApplyDashForce(bool instantCharge = false)
    {
        if (instantCharge)
        {
            // Cinematic push: skip windup and pathfinding entirely.
            // Use agent.Move() for direct per-frame movement so NavMesh recalculation
            // (triggered by door unlock) can't block the runner from moving.
            enemy.anim.SetBool("DashAttack", true);
            enemy.anim.SetBool("isCharging", true);
            enemy.agent.isStopped = false;
            enemy.agent.ResetPath(); // Don't queue a path request at all

            yield return null; // One frame to let agent settle

            DashAnimStarted?.Invoke();
            DashStarted?.Invoke();

            // Drive movement directly via agent.Move() — no pathfinding required
            float directMoveTimeout = enemy.stats.dashAttackDuration;
            float elapsed = 0f;

            while (elapsed < directMoveTimeout)
            {
                yield return null;
                elapsed += Time.deltaTime;

                Vector3 currentPlayerPos = Player.instance.transform.position;
                float distToPlayer = Vector3.Distance(enemy.transform.position, currentPlayerPos);

                // For cinematic push: keep closing the gap until right on the player
                // so CinematicKnockdownSequence.Co_WaitForHit() can detect the hit
                if (distToPlayer <= 1f)
                {
                    enemy.anim.SetBool("isCharging", false);
                    break;
                }

                Vector3 dir = (currentPlayerPos - enemy.transform.position);
                dir.y = 0f;
                dir.Normalize();

                // Rotate toward player
                if (dir.sqrMagnitude > 0.01f)
                    enemy.transform.rotation = Quaternion.RotateTowards(
                        enemy.transform.rotation, Quaternion.LookRotation(dir), 720f * Time.deltaTime);

                // Move directly on NavMesh surface — no path needed
                enemy.agent.Move(dir * enemy.stats.dashSpeed * Time.deltaTime);

                AttemptToDamage();
            }

            enemy.agent.isStopped = true;
            enemy.anim.SetBool("isCharging", false);
            canLeave = true;
            DashEnded?.Invoke();
            yield break;
        }
        else
        {
            // Normal dash: windup animation first, then charge
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

            yield return new WaitForSeconds(enemy.stats.dashAnimTransitionDelay);
        }

        // Sprint past player
        bool playedAttackAnim = false;
        bool passedPlayer = false;
        float stuckTimer = 0f;
        float stuckThreshold = 1.5f; // If no movement after this many seconds, abort and complete dash
        
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
                // Update target — clamp to NavMesh so path never fails
                Vector3 directionToPlayer = currentPlayerPos - enemy.transform.position;
                directionToPlayer.y = 0;
                directionToPlayer.Normalize();
                Vector3 idealTarget = currentPlayerPos + directionToPlayer * enemy.stats.dashTargetOffset;

                UnityEngine.AI.NavMeshHit navHit;
                dashTargetPosition = UnityEngine.AI.NavMesh.SamplePosition(idealTarget, out navHit, 3f, UnityEngine.AI.NavMesh.AllAreas)
                    ? navHit.position
                    : currentPlayerPos;
                
                if (enemy.agent.isOnNavMesh)
                    enemy.agent.SetDestination(dashTargetPosition);

                // Stuck detection: stationary agent OR pathPending for too long
                bool agentNotMoving = enemy.agent.velocity.sqrMagnitude < 0.05f;
                if (agentNotMoving)
                {
                    stuckTimer += Time.deltaTime;
                    if (stuckTimer >= stuckThreshold)
                    {
                        Vector3 forcedPos = currentPlayerPos - directionToPlayer * enemy.stats.attackRange;
                        enemy.agent.Warp(forcedPos);
                        enemy.transform.rotation = Quaternion.LookRotation(directionToPlayer);
                        passedPlayer = true;
                    }
                }
                else
                {
                    stuckTimer = 0f;
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

    /// <summary>Marks this dash's damage as already consumed so it can't fire when disableDashDamage is cleared.</summary>
    public void ConsumeCurrentDashDamage() => damaged = true;

    void AttemptToDamage()
    {
        if (damaged) return;

        if (disableDashDamage) return;

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
