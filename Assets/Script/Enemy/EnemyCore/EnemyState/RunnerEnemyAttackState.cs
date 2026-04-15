using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunnerEnemyAttackState : EnemyState
{
    float readyToLeaveTime;
    Quaternion targetRotation;

    public RunnerEnemyAttackState(Enemy enemy, EnemyStateMachine sm) : base(enemy, sm) { }

    public override void Enter()
    {
        // Calculate leave time based on attack animation + small buffer
        float minimumTimeToLeave = enemy.stats.attackDelay + enemy.stats.attackCooldown - 0.1f;
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
        
        // Exit immediately after attack animation finishes (no looping)
        canLeave = Time.time > readyToLeaveTime;

        //Rotate enemy towards player
        targetRotation = Quaternion.LookRotation(Player.instance.transform.position - enemy.transform.position);
        enemy.transform.rotation = Quaternion.Lerp(enemy.transform.rotation, targetRotation, enemy.stats.rotationSmoothness * Time.deltaTime);
    }

    IEnumerator Co_PerformAttackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        PerformAttack();
    }

    void PerformAttack()
    {
        if (enemy.agent.isOnNavMesh)
            enemy.agent.isStopped = true;

        //Set Attack Index
        if(enemy.stats.attackIndexes.Length > 0)
        {
            enemy.anim.SetFloat("AttackIndex", enemy.stats.attackIndexes[Random.Range(0, enemy.stats.attackIndexes.Length)]);
        }

        //Trigger Attack
        enemy.anim.SetTrigger("Attack");

        //Play SFX
        enemy.PlaySoundEffect(enemy.stats.attackSFX);
    }
}
