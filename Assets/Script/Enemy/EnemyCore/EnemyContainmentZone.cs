using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyContainmentZone : MonoBehaviour
{
    [Header("Containment Settings")]
    [Tooltip("Enemies will not leave this zone")]
    public bool containEnemies = true;
    
    [Tooltip("If player leaves zone, enemies return to idle/wander state")]
    public bool deaggroWhenPlayerLeaves = true;
    
    [Header("Debug")]
    public bool showGizmos = true;
    public Color gizmoColor = new Color(1f, 0.5f, 0f, 0.3f);
    
    private HashSet<Enemy> containedEnemies = new HashSet<Enemy>();
    private Collider zoneCollider;
    private bool playerInZone = false;

    void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        if (zoneCollider == null)
        {
            Debug.LogError("EnemyContainmentZone requires a Collider component (set as trigger)!");
            enabled = false;
            return;
        }

        if (!zoneCollider.isTrigger)
        {
            Debug.LogWarning("EnemyContainmentZone collider should be set as trigger!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Track player
        if (other.GetComponent<Player>() != null)
        {
            playerInZone = true;
            return;
        }

        // Register enemies
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null && containEnemies)
        {
            if (!containedEnemies.Contains(enemy))
            {
                containedEnemies.Add(enemy);
                enemy.SetContainmentZone(this);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Track player
        if (other.GetComponent<Player>() != null)
        {
            playerInZone = false;
            
            // De-aggro enemies if player leaves
            if (deaggroWhenPlayerLeaves)
            {
                foreach (Enemy enemy in containedEnemies)
                {
                    if (enemy != null)
                        enemy.OnPlayerLeftContainmentZone();
                }
            }
            return;
        }
    }

    void Update()
    {
        if (!containEnemies) return;

        // Check each contained enemy
        List<Enemy> toRemove = new List<Enemy>();
        foreach (Enemy enemy in containedEnemies)
        {
            if (enemy == null || enemy.gameObject == null)
            {
                toRemove.Add(enemy);
                continue;
            }

            // Check if enemy is trying to leave the zone
            if (!IsPointInZone(enemy.transform.position))
            {
                // Enemy is outside, stop their movement
                if (enemy.agent != null && enemy.agent.isOnNavMesh)
                {
                    enemy.agent.isStopped = true;
                    
                    // Find closest point inside zone and move there
                    Vector3 closestPoint = zoneCollider.ClosestPoint(enemy.transform.position);
                    enemy.agent.SetDestination(closestPoint);
                    enemy.agent.isStopped = false;
                }
            }
        }

        // Remove destroyed enemies
        foreach (Enemy enemy in toRemove)
        {
            containedEnemies.Remove(enemy);
        }
    }

    public bool IsPointInZone(Vector3 point)
    {
        return zoneCollider.bounds.Contains(point);
    }

    public Vector3 GetClosestPointInZone(Vector3 point)
    {
        return zoneCollider.ClosestPoint(point);
    }

    public bool IsPlayerInZone()
    {
        return playerInZone;
    }

    public void RemoveEnemy(Enemy enemy)
    {
        containedEnemies.Remove(enemy);
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = gizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            
            if (col is BoxCollider)
            {
                BoxCollider box = col as BoxCollider;
                Gizmos.DrawCube(box.center, box.size);
            }
            else if (col is SphereCollider)
            {
                SphereCollider sphere = col as SphereCollider;
                Gizmos.DrawSphere(sphere.center, sphere.radius);
            }
        }
    }
}
