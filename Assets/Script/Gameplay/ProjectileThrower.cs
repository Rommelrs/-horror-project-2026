using UnityEngine;
using ToolBox.Pools;

public class ProjectileThrower : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform spawnPoint;
    
    [Header("Target Positions")]
    [SerializeField] private Transform[] targetPositions;
    
    [Header("Timing")]
    [SerializeField] private float throwInterval = 2f;
    [SerializeField] private bool autoThrow = false;
    
    [Header("Disable Condition")]
    [SerializeField] private GameObject requiredObject;
    
    private float nextThrowTime;
    private int currentTargetIndex = 0;
    private bool requiredObjectWasActive = false;

    private void Update()
    {
        // Track if the required object has been active at least once
        if (requiredObject != null && requiredObject.activeInHierarchy)
        {
            requiredObjectWasActive = true;
        }
        
        // Only disable if it WAS active before and now it's gone/inactive
        if (requiredObjectWasActive)
        {
            if (requiredObject == null || !requiredObject.activeInHierarchy)
            {
                enabled = false;
                return;
            }
        }
        
        // A checkpoint is still being restored: the trigger that switched this on is re-applied in the first frame, while its
        // dead enemy is only destroyed at the end of that frame - every thrower in Alley 1 threw once on load.
        if (CheckpointManager.IsLoadingCheckpoint)
            return;

        // Its enemy isn't here: never started, or restored dead by a checkpoint (it never "was active", so the rule above can't
        // fire). Without this a reload threw a bag at once, straight onto Khalid at the Coward meeting checkpoint.
        // (object) cast: a dead enemy restored by a checkpoint is DESTROYED, and a destroyed reference reads "== null" like an
        // empty field - that skipped every rule here and the thrower threw.
        if ((object)requiredObject != null && (requiredObject == null || !requiredObject.activeInHierarchy || RequiredIsDead()))
            return;

        if (autoThrow && Time.time >= nextThrowTime)
        {
            ThrowAtNextTarget();
            nextThrowTime = Time.time + throwInterval;
        }
    }

    Health requiredHealth;
    bool RequiredIsDead()
    {
        if (requiredHealth == null) requiredHealth = requiredObject.GetComponent<Health>();
        return requiredHealth != null && requiredHealth.IsDead;
    }

    public void SetRequiredObject(GameObject obj)
    {
        requiredObject = obj;
        requiredHealth = null;
        requiredObjectWasActive = false;
    }

    // Throw at the next target in the array (cycles through)
    public void ThrowAtNextTarget()
    {
        if (targetPositions == null || targetPositions.Length == 0)
            return;

        ThrowAtTarget(currentTargetIndex);
        currentTargetIndex = (currentTargetIndex + 1) % targetPositions.Length;
    }

    // Throw at a specific target index
    public void ThrowAtTarget(int targetIndex)
    {
        if (targetPositions == null || targetIndex >= targetPositions.Length)
            return;

        ThrowAtPosition(targetPositions[targetIndex].position);
    }

    // Throw at a specific world position
    public void ThrowAtPosition(Vector3 targetPosition)
    {
        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        
        GameObject projectileObj = projectilePrefab.Reuse(spawnPos, Quaternion.identity);
        Projectile projectile = projectileObj.GetComponent<Projectile>();
        
        if (projectile != null)
        {
            projectile.ShootProjectileAtPosition(targetPosition);
        }
    }

    // Throw at all targets at once
    public void ThrowAtAllTargets()
    {
        if (targetPositions == null)
            return;

        for (int i = 0; i < targetPositions.Length; i++)
        {
            ThrowAtPosition(targetPositions[i].position);
        }
    }

    // Throw at a random target
    public void ThrowAtRandomTarget()
    {
        if (targetPositions == null || targetPositions.Length == 0)
            return;

        int randomIndex = Random.Range(0, targetPositions.Length);
        ThrowAtTarget(randomIndex);
    }

    // Visualize targets in editor
    private void OnDrawGizmosSelected()
    {
        if (targetPositions == null)
            return;

        Gizmos.color = Color.red;
        foreach (var target in targetPositions)
        {
            if (target != null)
            {
                Gizmos.DrawWireSphere(target.position, 0.5f);
                
                // Draw line from spawn point to target
                Vector3 start = spawnPoint != null ? spawnPoint.position : transform.position;
                Gizmos.DrawLine(start, target.position);
            }
        }
    }
}
