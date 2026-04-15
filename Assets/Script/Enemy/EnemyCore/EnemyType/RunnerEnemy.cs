using System.Collections;
using System.Collections.Generic;
using ToolBox.Pools;
using UnityEngine;
using UnityEngine.AI;

public class RunnerEnemy : Enemy
{
    public EnemySidestepState enemySidestepState;
    public EnemyDashAttackState enemyDashAttackState;
    public RunnerEnemyAttackState runnerAttackState;
    public EnemyWeakpointHurtState weakpointHurtState;
    public Transform leftKneeTransform;
    public Transform rightKneeTransform;

    public float sideStepRange = 8f;

    public TrailRenderer dashTrailRenderer;
    public ParticleSystem dashPS;
    public float sideStepInjuredSpeed = 1f;
    public float injuredMovementSpeed = 1f;
    public bool playLookAroundAtWaypoints = true;

    [HideInInspector] public bool sideStepToRight = true;
    [HideInInspector] public bool leftKneeDamaged = false;
    [HideInInspector] public int weakpointHitCount = 0; // 0 = no hits, 1 = first hit, 2 = second hit

    int sideStepCount = 0;
    bool injured = false;

    float defaultMoveSpeed;
    float defaultSideStepSpeed;

    public override void Awake()
    {
        base.Awake();
        
        // Create custom attack state that doesn't loop attacks
        runnerAttackState = new RunnerEnemyAttackState(this, stateMachine);

        enemySidestepState = new EnemySidestepState(this, stateMachine);
        enemyDashAttackState = new EnemyDashAttackState(this, stateMachine);
        weakpointHurtState = new EnemyWeakpointHurtState(this, stateMachine);
        
        // Override death state with Runner-specific version (workaround for broken Die animation)
        deathState = new RunnerEnemyDeathState(this, stateMachine);
    }

    public override void Start()
    {
        base.Start();

        //Save Default Side step speed
        defaultMoveSpeed = stats.movementSpeed;
        defaultSideStepSpeed = stats.sideStepSpeed;
        
        // Initialize animator speed parameter
        if (anim != null)
        {
            anim.SetFloat("Speed", 0f);
        }

        if (enemyDashAttackState != null)
        {
            enemyDashAttackState.DashAnimStarted += DashAnimationStarted;
            enemyDashAttackState.DashStarted += InitDashTrail;
            enemyDashAttackState.DashEnded+= StopDashTrail;
        }

        if(enemyWanderState != null)
        {
            enemyWanderState.OnWaypointReached += OnWaypointReached;
        }

        //Reset Left Knee Damaged
        leftKneeDamaged = false;

        //Set Enemy Weakpoint Bone Transform - both knees for sequential alternation
        List<Transform> newBoneTransform = new List<Transform>();
        newBoneTransform.Add(leftKneeTransform);
        newBoneTransform.Add(rightKneeTransform);
        enemyWeakpoint.SetBoneTransform(newBoneTransform.ToArray());

        //Reset Injured
        injured = false;
        anim.SetInteger("Injured", 0);
    }

    void SetInjuredSpeed()
    {
        stats.movementSpeed = injuredMovementSpeed;
        stats.sideStepSpeed = sideStepInjuredSpeed;
    }

    void SetDefaultSpeed()
    {
        stats.movementSpeed = defaultMoveSpeed;
        stats.sideStepSpeed = defaultSideStepSpeed;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (enemyDashAttackState != null)
        {
            enemyDashAttackState.DashAnimStarted -= DashAnimationStarted;
            enemyDashAttackState.DashStarted -= InitDashTrail;
            enemyDashAttackState.DashEnded -= StopDashTrail;
        }

        if (enemyWanderState != null)
        {
            enemyWanderState.OnWaypointReached -= OnWaypointReached;
        }
    }

    public void DashAnimationStarted()
    {
        if (enemyWeakpoint != null)
        {
            enemyWeakpoint.SpawnEnemyWeakpoint();
        }
    }

    public void InitDashTrail()
    {
        if (dashTrailRenderer != null)
            dashTrailRenderer.emitting = true;

        if (dashPS != null)
            dashPS.Play();
    }

    public void StopDashTrail()
    {
        if (dashTrailRenderer != null)
            dashTrailRenderer.emitting = false;

        if (dashPS != null && dashPS.isPlaying)
            dashPS.Stop();

        enemyWeakpoint.DestorySpawnedWeakpoint();
    }

    public void OnWaypointReached()
    {
        if (playLookAroundAtWaypoints && stats.idleHoldBetweenWaypointDuration > 0)
        {
            //Trigger left and right look animation
            anim.SetTrigger("LookAround");
        }
    }

    //Attack successfully called from Animation Event
    public override void AttackHit()
    {
        if (GameManager.IsPaused) return;

        if (health.IsDead) return;

        if (PlayerInRange(stats.attackRange) && PlayerInFOV())
        {
            //Successful hit
            Player.instance.health.Damage(stats.damage);
            
            // Play stab sound effect (2D)
            Play2DSoundEffect(stats.stabSFX);
        }
    }

    public override void TakeDamage(int dmgValue)
    {
        if (health.IsDead) return;

        if (health.isDamageByWeakpointHit)
        {
            
            //Knockback State
            Vector3 playerPos = Player.instance.transform.position;
            playerPos.y = transform.position.y;
            Vector3 damageDirection = transform.position - playerPos;
            this.damageDirection = damageDirection;

            if (health.isDamageByWeakpointHit)
                this.damageForceMultiplier = Player.instance.playerWeaponSystem.weakpointHitEnemyKnockbackMultiplier;
            else
                this.damageForceMultiplier = 1f;

            if (!leftKneeDamaged)
            {
                //Left Knee Weakpoint Hit (FIRST HIT)
                leftKneeDamaged = true;
                weakpointHitCount = 1;
                anim.SetFloat("InjuredKneeDirection", 0);

                injured = true;
                anim.SetInteger("Injured", 1);

                SetInjuredSpeed();
                // Advance to next weakpoint (right knee)
                enemyWeakpoint.AdvanceSequentialIndex();
            }
            else
            {
                //Right Knee Weakpoint Hit (SECOND HIT)
                weakpointHitCount = 2;
                anim.SetFloat("InjuredKneeDirection", 1);

                // Reset cycle - go back to left knee
                leftKneeDamaged = false;

                injured = true;
                anim.SetInteger("Injured", 1);

                SetInjuredSpeed();
                // Advance to next weakpoint (cycles back to left knee)
                enemyWeakpoint.AdvanceSequentialIndex();
            }
            
            //Change to Weakpoint Hurt State
            stateMachine.ChangeState(weakpointHurtState);

            PlaySoundEffect(stats.takeDamageSFX);
        }
        else
        {
            if(stateMachine.CurrentState == enemyDashAttackState)
            {
                //Only Play SFX
                PlaySoundEffect(stats.takeDamageSFX);
                
                // Reset Knockback trigger to prevent animator from getting stuck
                anim.ResetTrigger("Knockback");

                return;
            }
            
            //Trigger knockback Animation
            anim.SetTrigger("Knockback");

            //Only Play SFX
            PlaySoundEffect(stats.takeDamageSFX);

            //Attempt to instanly trigger dash attack State
            StartCoroutine(Co_AttempToTriggerDashAttackAfterGettingHit());
        }
    }

    IEnumerator Co_AttempToTriggerDashAttackAfterGettingHit()
    {
        yield return new WaitForSeconds(0.3f);

        // Only trigger if still in sidestep state
        if (stateMachine.CurrentState == enemySidestepState)
        {
            //If Enemy SideSteping and gets hit then instantly trigger dash attack
            sideStepCount = 0;
            stateMachine.ChangeState(enemyDashAttackState);
        }
    }

    public override void OnPool()
    {
        base.OnPool();

        StopDashTrail();

        //Reset Left Knee Damaged
        leftKneeDamaged = false;
        weakpointHitCount = 0;

        //Set Enemy Weakpoint Bone Transform - both knees for sequential alternation
        List<Transform> newBoneTransform = new List<Transform>();
        newBoneTransform.Add(leftKneeTransform);
        newBoneTransform.Add(rightKneeTransform);
        enemyWeakpoint.SetBoneTransform(newBoneTransform.ToArray());

        //Reset Injured
        injured = false;
        anim.SetInteger("Injured", 0);

        enemyWeakpoint.DestorySpawnedWeakpoint();

        SetDefaultSpeed();
    }

    public override void CheckLeaveCondition(EnemyState currentState)
    {
        //Currently in Idle State
        if (currentState == idleState)
        {
            //Ready to Leave
            if (currentState.canLeave)
            {
                if (stats.enemyType == EnemyType.Aggressive)
                {
                    //Aggressive Enemy Type
                    stateMachine.ChangeState(chaseState);
                }
                else if (stats.enemyType == EnemyType.Wandering)
                {
                    //Wandering Enemy Type
                    // Detect player in vision range
                    if (PlayerInRange(stats.visionRadius))
                    {
                        stateMachine.ChangeState(chaseState);
                    }
                    else
                    {
                        stateMachine.ChangeState(enemyWanderState);
                    }
                }
            }
        }

        //Currently in Wander State
        if (currentState == enemyWanderState)
        {
            if (stats.enemyType == EnemyType.Wandering || stats.enemyType == EnemyType.Aggressive)
            {
                //If within vision radius
                if (PlayerInRange(stats.visionRadius))
                {
                    stateMachine.ChangeState(chaseState);
                }
            }
        }

        //Currently in EnemyChaseState
        if (currentState == chaseState)
        {
            if (currentState.canLeave)
            {
                // If within sidestep range
                if (PlayerInRange(sideStepRange))
                {
                    sideStepCount++;
                    stateMachine.ChangeState(enemySidestepState);
                }
            }
        }

        //Sidestep State
        if (currentState == enemySidestepState)
        {
            if (currentState.canLeave)
            {
                if (!PlayerInRange(stats.visionRadius))
                {
                    stateMachine.ChangeState(chaseState);
                }
                else
                {
                    //Dash Attack or continue EnemySidestep
                    if(sideStepCount >= 1)
                    {
                        sideStepCount = 0;
                        
                        // If player is very close (within attack range), use simple attack instead of dash
                        if (PlayerInRange(stats.attackRange))
                        {
                            stateMachine.ChangeState(runnerAttackState);
                        }
                        else
                        {
                            stateMachine.ChangeState(enemyDashAttackState);
                        }
                    }
                    else
                    {
                        sideStepCount ++;
                        stateMachine.ChangeState(enemySidestepState);
                    }
                }
            }
        }

        //Attack State
        if (currentState == runnerAttackState)
        {
            if (currentState.canLeave)
            {
                // Always exit after one attack - don't stay in attack state
                if (!PlayerInRange(stats.visionRadius))
                {
                    stateMachine.ChangeState(chaseState);
                }
                else
                {
                    //Continue Sidestep after attack
                    sideStepCount++;
                    stateMachine.ChangeState(enemySidestepState);
                }
            }
        }

        //DashAttack State
        if (currentState == enemyDashAttackState)
        {
            if (currentState.canLeave)
            {
                if (!PlayerInRange(stats.visionRadius))
                {
                    stateMachine.ChangeState(chaseState);
                }
                else
                {
                    //Continue Sidestep
                    sideStepCount++;
                    stateMachine.ChangeState(enemySidestepState);
                }
            }
        }
        
        //Weakpoint Hurt State
        if (currentState == weakpointHurtState)
        {
            if (currentState.canLeave)
            {
                if (!PlayerInRange(stats.visionRadius))
                {
                    stateMachine.ChangeState(chaseState);
                }
                else
                {
                    stateMachine.ChangeState(chaseState);
                }
            }
        }
    }

    public override void AlertToSound(Vector3 soundPosition)
    {
        // Don't interrupt dash attack with sound alerts
        if (stateMachine.CurrentState == enemyDashAttackState)
            return;
        
        base.AlertToSound(soundPosition);
    }

    public override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        //Show Side Step Radius 
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, sideStepRange);
    }
}
