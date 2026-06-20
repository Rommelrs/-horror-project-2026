using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyReloadState : EnemyState
{
    BagBearerEnemy bagBearerEnemy;

    public EnemyReloadState(BagBearerEnemy enemy, EnemyStateMachine sm) : base(enemy, sm) 
    {
        bagBearerEnemy = enemy;
    }

    Coroutine reloadCR;

    public override void Enter()
    {
        base.Enter();

        if (enemy.agent.isOnNavMesh)
            enemy.agent.isStopped = true;

        canLeave = false;

        if (reloadCR != null) enemy.StopCoroutine(reloadCR);
        reloadCR = enemy.StartCoroutine(Co_Reload());
    }

    IEnumerator Co_Reload()
    {
        yield return null;

        for (int i = 0; i < bagBearerEnemy.bagBearerReloadGroup.Length; i++)
        {
            if (bagBearerEnemy.bagBearerReloadGroup[i].isReloaded == false)
            {
                enemy.anim.SetFloat("ReloadIndex", bagBearerEnemy.bagBearerReloadGroup[i].reloadIndex);
                enemy.anim.SetTrigger("Reload");

                yield return new WaitForSeconds(bagBearerEnemy.bagBearerReloadGroup[i].weakpointShowDelay);

                //Spawn Weakpoints
                bagBearerEnemy.enemyWeakpoint.SpawnEnemyWeakpoint();

                yield return new WaitForSeconds(bagBearerEnemy.bagBearerReloadGroup[i].reloadDurationStart);

                bagBearerEnemy.bagBearerReloadGroup[i].isReloaded = true;
                bagBearerEnemy.bagBearerReloadGroup[i].reloadObject.SetActive(true);

                yield return new WaitForSeconds(bagBearerEnemy.bagBearerReloadGroup[i].reloadDurationEnd);

                //Destory Spawned Weakpoints
                bagBearerEnemy.enemyWeakpoint.DestorySpawnedWeakpoint();
            }
        }

        canLeave = true;
    }

    public override void Exit()
    {
        base.Exit();

        if (reloadCR != null) enemy.StopCoroutine(reloadCR);

        //Destory Spawned Weakpoints
        bagBearerEnemy.enemyWeakpoint.DestorySpawnedWeakpoint();
    }

    public override void Update()
    {
        base.Update();

        if (enemy.agent.isOnNavMesh)
        {
            enemy.agent.isStopped = true;
            
            // Force Fixed enemies to stay completely still
            if (enemy.stats.enemyType == EnemyType.Fixed)
            {
                enemy.agent.velocity = Vector3.zero;
            }
        }

        
        if (enemy.stats.enemyType == EnemyType.Fixed)
        {
            if (canLeave)
            {
                stateMachine.ChangeState(enemy.idleState);
            }
        }
        else 
        {
            if (canLeave)
            {
                stateMachine.ChangeState(enemy.chaseState);
            }
        }
    }
}
