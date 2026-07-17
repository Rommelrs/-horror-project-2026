using UnityEngine;

public class EnemyAggroTrigger : MonoBehaviour
{
    [SerializeField] private Enemy[] enemies;
    [SerializeField] private bool destroyTriggerAfterActivation = true;
    
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        // Only care about the player
        if (!other.CompareTag("Player")) return;

        Debug.Log($"[AggroTrigger] Player entered | hasTriggered: {hasTriggered}");

        if (hasTriggered) return;
        
        // Check save system
        SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
        if (saveableTrigger != null && saveableTrigger.WasAlreadyTriggered())
        {
            Debug.Log("[AggroTrigger] Already triggered in save system, skipping");
            hasTriggered = true;
            return;
        }
                {
            hasTriggered = true;
            
            // Mark as triggered in save system
            if (saveableTrigger != null)
            {
                saveableTrigger.MarkAsTriggered();
            }
            
            // Activate all enemies
            if (enemies != null && enemies.Length > 0)
            {
                foreach (Enemy enemy in enemies)
                {
                    if (enemy != null)
                    {
                        if (!enemy.gameObject.activeInHierarchy)
                        {
                            Debug.Log($"[AggroTrigger] Activating inactive enemy: {enemy.name}");
                            enemy.gameObject.SetActive(true);
                        }

                        Debug.Log($"[AggroTrigger] Setting {enemy.name} to Aggressive + Chase");
                        
                        // Use ChangeEnemyType for all enemies (handles state transitions properly)
                        enemy.ChangeEnemyType(EnemyType.Aggressive);
                    }
                    else
                    {
                        Debug.LogWarning("[AggroTrigger] Enemy reference is NULL in array!");
                    }
                }
            }

            if (destroyTriggerAfterActivation)
                Destroy(gameObject);
        }
    }
}
