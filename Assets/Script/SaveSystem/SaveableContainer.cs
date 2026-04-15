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
        // Check with SaveManager if this container was opened
        if (SaveManager.instance != null && SaveManager.instance.IsContainerOpened(uniqueID.ID))
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
