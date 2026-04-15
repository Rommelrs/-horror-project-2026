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

    IEnumerator Co_SidestepCheck()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            //Check if ready to sidestep
            if (Time.time > lastSideStepTime)
            {
                //bool playerIsLookingAtEnemy = IsPlayerLookingAtEnemy(Player.instance.transform, this.transform, 40);

                //Check if player is inside enemy vision area but NOT in attack range
                if (Vector3.Distance(transform.position, Player.instance.transform.position) < stats.visionRadius
                    && !PlayerInRange(stats.attackRange))
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

    public override void CheckLeaveCondition(EnemyState currentState)
    {
        base.CheckLeaveCondition(currentState);

        //Sidestep State
        if (currentState == enemySidestepState)
        {
            if (currentState.canLeave)
            {
                if (PlayerInRange(stats.attackRange))
                {
                    stateMachine.ChangeState(attackState);
                    return;
                }

                if (!PlayerInRange(stats.attackRange))
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
