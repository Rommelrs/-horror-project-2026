using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAttackState : EnemyState
{
    float attackCooldown;
    float readyToLeaveTime;
    Quaternion targetRotation;

    public EnemyAttackState(Enemy enemy, EnemyStateMachine sm) : base(enemy, sm) { }

    public override void Enter()
    {
        attackCooldown = enemy.stats.attackCooldown;

        float minimumTimeToLeave = attackCooldown - 0.1f + enemy.stats.attackDelay;
        readyToLeaveTime = Time.time + minimumTimeToLeave;

        if (enemy.agent.isOnNavMesh)
            enemy.agent.isStopped = true;

        if (enemy.stats.attackDelay <= 0)
            PerformAttack();
        else
            enemy.StartCoroutine(Co_PerformAttackAfterDelay(enemy.stats.attackDelay));

        //Trigger Event
        enemy.OnAttackStarted?.Invoke();
    }

    public override void Exit()
    {
         base.Exit();

         enemy.anim.ResetTrigger("Attack");
    }

    public override void Update()
    {
        enemy.CheckLeaveCondition(this);
        canLeave = Time.time > readyToLeaveTime;

        // Force Fixed enemies to stay completely still
        if (enemy.stats.enemyType == EnemyType.Fixed && enemy.agent.isOnNavMesh)
        {
            enemy.agent.velocity = Vector3.zero;
        }

        ////Look at player if ranged attack type enemy
        //if (enemy.stats.attackType == AttackType.Ranged)
        //{
        //    targetRotation = Quaternion.LookRotation(Player.instance.transform.position - enemy.transform.position);
        //    enemy.transform.rotation = Quaternion.Lerp(enemy.transform.rotation, targetRotation, enemy.stats.rotationSmoothness * Time.deltaTime);
        //}

        //Rotate enemy towards player
        targetRotation = Quaternion.LookRotation(Player.instance.transform.position - enemy.transform.position);
        enemy.transform.rotation = Quaternion.Lerp(enemy.transform.rotation, targetRotation, enemy.stats.rotationSmoothness * Time.deltaTime);

        attackCooldown -= Time.deltaTime;
        if (attackCooldown <= 0)
        {
            PerformAttack();
            attackCooldown = enemy.stats.attackCooldown;
        }
    }

    IEnumerator Co_PerformAttackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        PerformAttack();
    }

    void PerformAttack()
    {
        attackCooldown = enemy.stats.attackCooldown;

        if (enemy.agent.isOnNavMesh)
            enemy.agent.isStopped = true;

        //Set Attack Index
        if(enemy.stats.attackIndexes.Length > 0)
        {
            enemy.anim.SetFloat("AttackIndex", enemy.stats.attackIndexes[Random.Range(0, enemy.stats.attackIndexes.Length)]);
        }

        //BagBearer Enemy Type Custom Function
        if (enemy is BagBearerEnemy bagBearerEnemy)
        {
            for (int i = 0; i < bagBearerEnemy.bagBearerReloadGroup.Length; i++)
            {
                if (bagBearerEnemy.bagBearerReloadGroup[i].isReloaded == true)
                {
                    enemy.anim.SetFloat("AttackIndex", bagBearerEnemy.bagBearerReloadGroup[i].reloadIndex);
                    break;
                }
            }
        }

        //Trigger Attack
        enemy.anim.SetTrigger("Attack");

        //Play SFX
        enemy.PlaySoundEffect(enemy.stats.attackSFX);
    }
}
