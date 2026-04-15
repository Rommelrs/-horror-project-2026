using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class EnemyWanderState : EnemyState
{
    public EnemyWanderState(Enemy enemy, EnemyStateMachine sm) : base(enemy, sm) { }

    private int waypointIndex = 0;
    private float nextTimeToMove;

    private Quaternion targetRotation;
    public Action OnWaypointReached;
    public Action OnWaypointEndReached;

    public override void Enter()
    {
        enemy.agent.isStopped = false;

        if (!enemy.agent.enabled)
            enemy.agent.enabled = true;

        if (!enemy.agent.isOnNavMesh)
            enemy.agent.Warp(enemy.transform.position);

        //Set Agent speed
        enemy.agent.speed = enemy.stats.wanderingSpeed;
    }

    public override void Update()
    {
        enemy.CheckLeaveCondition(this);

        if (enemy.stats.waypoints != null && enemy.stats.waypoints.Length > 0)
        {
            //Apply rotation
            enemy.transform.rotation = Quaternion.Lerp(enemy.transform.rotation, targetRotation, enemy.stats.rotationSmoothness * Time.deltaTime);

            //Check if already reached current waypoint
            float dst = Mathf.Abs(Vector3.Distance(enemy.transform.position, enemy.stats.waypoints[waypointIndex].position));
            if (dst < 0.3f + enemy.agent.stoppingDistance)
            {
                //Face the waypoint's forward direction when reached
                targetRotation = enemy.stats.waypoints[waypointIndex].rotation;
                
                //Reach waypoint
                waypointIndex++;
                if (waypointIndex >= enemy.stats.waypoints.Length)
                {
                    OnWaypointEndReached?.Invoke();
                    waypointIndex = 0;
                }

                if (enemy.stats.idleHoldBetweenWaypointDuration > 0)
                    nextTimeToMove = Time.time + enemy.stats.idleHoldBetweenWaypointDuration;

                OnWaypointReached?.Invoke();
            }

            //If player is ready to move towards new waypiont
            if (Time.time > nextTimeToMove)
            {
                //Start Moving to current Waypoint
                Vector3 direction = enemy.stats.waypoints[waypointIndex].position - enemy.transform.position;
                if (direction.sqrMagnitude > 0.001f) // Only update rotation if there's a valid direction
                {
                    targetRotation = Quaternion.LookRotation(direction);
                }
                MoveEnemy(enemy.stats.waypoints[waypointIndex].position);
            }
        }
    }

    void MoveEnemy(Vector3 movePosition)
    {
        if (enemy.agent.isOnNavMesh)
        {
            enemy.agent.SetDestination(movePosition);
            enemy.agent.isStopped = false;
        }
        else
        {
            enemy.agent.Warp(enemy.transform.position);
        }
    }
}
