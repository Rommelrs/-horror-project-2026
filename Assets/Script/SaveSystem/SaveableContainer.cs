using UnityEngine;

/// <summary>
/// Makes a loot container saveable - tracks if it was opened
/// Add this component to LootContainer GameObjects
/// Also needs UniqueID component
/// </summary>
[RequireComponent(typeof(UniqueID))]
[RequireComponent(typeof(LootContainer))]
public class SaveableContainer : MonoBehaviour, ISaveable
{
    private UniqueID uniqueID;
    private LootContainer lootContainer;
    
    private void Awake()
    {
        uniqueID = GetComponent<UniqueID>();
        lootContainer = GetComponent<LootContainer>();
    }
    
    private void Start()
    {
        // Same as SaveableEnemy: a checkpoint restore never calls Load(), and Load() only reaches active objects
        ApplySavedState();
    }

    /// <summary>
    /// Call this when the container is opened
    /// </summary>
    public void MarkAsOpened()
    {
        // Immediately register with SaveManager
        if (SaveManager.instance != null)
        {
            SaveManager.instance.RegisterOpenedContainer(uniqueID.ID);
        }
    }
    
    public void Save(SaveData saveData)
    {
        // Containers are tracked by SaveManager runtime tracker - nothing to do here
    }
    
    public void Load(SaveData saveData)
    {
        ApplySavedState();
    }

    void ApplySavedState()
    {
        // Check with SaveManager if this container was opened
        if (SaveManager.instance != null && uniqueID != null && SaveManager.instance.IsContainerOpened(uniqueID.ID))
        {
            Debug.Log($"Container was already opened, restoring open state: {gameObject.name}");
            
            // Restore the container to its opened state without spawning loot
            if (lootContainer != null)
            {
                lootContainer.RestoreOpenedState();
            }
        }
    }
}
