using UnityEngine;

/// <summary>
/// Makes an enemy spawner saveable - tracks if it was stopped
/// Add this component to ItemPickupEnemySpawner GameObjects
/// Also needs UniqueID component
/// </summary>
[RequireComponent(typeof(UniqueID))]
[RequireComponent(typeof(ItemPickupEnemySpawner))]
public class SaveableSpawner : MonoBehaviour, ISaveable
{
    private UniqueID uniqueID;
    private ItemPickupEnemySpawner spawner;
    
    private void Awake()
    {
        uniqueID = GetComponent<UniqueID>();
        spawner = GetComponent<ItemPickupEnemySpawner>();
    }
    
    /// <summary>
    /// Call this when the spawner is stopped
    /// </summary>
    public void MarkAsStopped()
    {
        Debug.Log($"Spawner stopped: {gameObject.name} ({uniqueID.ID})");
        
        // Immediately register with SaveManager
        if (SaveManager.instance != null)
        {
            SaveManager.instance.RegisterStoppedSpawner(uniqueID.ID);
        }
    }
    
    public void Save(SaveData saveData)
    {
        // Spawners are tracked by SaveManager runtime tracker - nothing to do here
    }
    
    public void Load(SaveData saveData)
    {
        Debug.Log($"Load() called for spawner {gameObject.name} with ID: {uniqueID.ID}");
        
        // Check with SaveManager if this spawner was stopped
        if (SaveManager.instance != null)
        {
            bool wasStopped = SaveManager.instance.IsSpawnerStopped(uniqueID.ID);
            Debug.Log($"Checking if {gameObject.name} ({uniqueID.ID}) was stopped: {wasStopped}");
            
            if (wasStopped)
            {
                Debug.Log($"Spawner was already stopped, disabling: {gameObject.name}");
                
                // Stop the spawner and despawn enemies
                if (spawner != null)
                {
                    spawner.StopSpawningAndDespawn();
                }
                
                // Disable this GameObject so it doesn't trigger again
                gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.LogWarning($"SaveManager.instance is null for spawner {gameObject.name}");
        }
    }
}
