using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAddonCustomMoveToSpot : MonoBehaviour
{
    [SerializeField] Transform []waypoints;
    [SerializeField] float moveSpeed = 2f;
    [SerializeField] float lookAroundDelay = 3f;
    [SerializeField] bool playLookAroundOnDetect = true;
    [SerializeField] float idleAtWaypoints = 0f; // Idle duration at each waypoint (0 = no idle)
    [SerializeField] float idleAtFinalWaypoint = 0f; // Idle duration at final waypoint before destroying (0 = destroy immediately)
    [SerializeField] AudioClip startClip;
    [SerializeField] AudioClip waypointReachedClip; // Plays at each waypoint
    [SerializeField] AudioClip finalWaypointReachedClip; // Plays at final waypoint

    Enemy enemy;
    bool playerDetected = false;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    private void Start()
    {
        enemy.onPlayerDetected.AddListener(OnPlayerDetected);
        enemy.enemyWanderState.OnWaypointReached += OnWaypointReached;
        enemy.enemyWanderState.OnWaypointEndReached += OnReachedToSpot;
    }

    private void OnDestroy()
    {
        enemy.onPlayerDetected.RemoveListener(OnPlayerDetected);
        enemy.enemyWanderState.OnWaypointReached -= OnWaypointReached;
        enemy.enemyWanderState.OnWaypointEndReached -= OnReachedToSpot;
    }

    public void OnPlayerDetected()
    {
        if (!playerDetected)
        {
            playerDetected = true;

            if(SoundEffectManager.instance)
                SoundEffectManager.instance.PlaySFX(startClip);

            StartCoroutine(Co_PlayerDetected());
        }
    }

    IEnumerator Co_PlayerDetected()
    {
        //Start Moving to spot
        enemy.stats.wanderingSpeed = moveSpeed;
        enemy.stats.waypoints = waypoints;
        enemy.stats.idleHoldBetweenWaypointDuration = idleAtWaypoints;
        enemy.stateMachine.ChangeState(enemy.enemyWanderState);
        yield break;
    }

    private void OnWaypointReached()
    {
        // Play sound at each waypoint (not final)
        if (waypointReachedClip != null && SoundEffectManager.instance)
        {
            SoundEffectManager.instance.PlaySFX(waypointReachedClip);
        }
    }

    private void OnReachedToSpot()
    {
        StartCoroutine(Co_OnReachedToSpot());
    }

    IEnumerator Co_OnReachedToSpot()
    {
        // Play sound at final waypoint
        if (finalWaypointReachedClip != null && SoundEffectManager.instance)
        {
            SoundEffectManager.instance.PlaySFX(finalWaypointReachedClip);
        }

        // Wait at final waypoint if set
        if (idleAtFinalWaypoint > 0f)
        {
            yield return new WaitForSeconds(idleAtFinalWaypoint);
        }

        // Register as dead with save system before destroying
        SaveableEnemy saveableEnemy = GetComponent<SaveableEnemy>();
        if (saveableEnemy != null)
        {
            // Use SaveManager directly to register as dead
            if (SaveManager.instance != null)
            {
                SaveManager.instance.RegisterDeadEnemy(saveableEnemy.GetComponent<UniqueID>().ID);
                Debug.Log($"Enemy reached destination and registered as dead: {gameObject.name}");
            }
        }
        
        Destroy(this.gameObject);
    }
}
