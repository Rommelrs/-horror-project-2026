using UnityEngine;

public class SpawnerStopTrigger : MonoBehaviour
{
    [Header("Required Item")]
    [SerializeField] private Item requiredItem;

    [Header("Spawner To Stop")]
    [SerializeField] private ItemPickupEnemySpawner spawnerToStop;
    [SerializeField] private bool stopMusic = true;
    [SerializeField] private bool despawnEnemies = true;

    [Header("Enemy To Activate")]
    [SerializeField] private GameObject enemyToActivate;

    [Header("GameObjects To Disable")]
    [SerializeField] private GameObject[] objectsToDisable;

    [Header("Settings")]
    [SerializeField] private bool destroyAfterTrigger = true;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag("Player")) return;
        
        // Check save system
        SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
        if (saveableTrigger != null && saveableTrigger.WasAlreadyTriggered())
        {
            hasTriggered = true;
            return;
        }
        
        if (!PlayerHasItem()) return;

        hasTriggered = true;
        
        // Mark as triggered in save system
        if (saveableTrigger != null)
        {
            saveableTrigger.MarkAsTriggered();
        }

        // Stop spawner
        if (spawnerToStop != null)
        {
            if (despawnEnemies)
            {
                spawnerToStop.StopSpawningAndDespawn();
            }
            else
            {
                spawnerToStop.StopSpawning();
            }
            
            if (stopMusic)
                spawnerToStop.StopMusic();
        }

        // Activate enemy
        if (enemyToActivate != null)
        {
            enemyToActivate.SetActive(true);
        }

        // Disable objects
        if (objectsToDisable != null)
        {
            foreach (var obj in objectsToDisable)
            {
                if (obj != null)
                    obj.SetActive(false);
            }
        }

        if (destroyAfterTrigger)
            Destroy(gameObject);
    }

    private bool PlayerHasItem()
    {
        if (requiredItem == null) return true;

        if (Player.instance == null || Player.instance.inventory == null) return false;

        foreach (var stack in Player.instance.inventory.GetItems())
        {
            if (stack.item == requiredItem) return true;
        }

        return false;
    }
}
