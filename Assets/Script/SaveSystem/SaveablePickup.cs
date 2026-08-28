using UnityEngine;

/// <summary>
/// Makes an item pickup saveable - tracks if it was picked up
/// Add this component to ItemPickup or InspectableItemPickup GameObjects
/// Also needs UniqueID component
/// </summary>
[RequireComponent(typeof(UniqueID))]
public class SaveablePickup : MonoBehaviour, ISaveable
{
    private UniqueID uniqueID;
    
    private void Awake()
    {
        uniqueID = GetComponent<UniqueID>();
    }

    private void Start()
    {
        // Check runtime state on scene load (handles scene transitions without explicit LoadGame)
        if (SaveManager.instance != null && SaveManager.instance.IsItemPickedUp(uniqueID.ID))
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Call this when the item is picked up
    /// </summary>
    public void MarkAsPickedUp()
    {
        
        // Immediately register this pickup with SaveManager
        if (SaveManager.instance != null)
        {
            SaveManager.instance.RegisterPickedUpItem(uniqueID.ID);
        }
    }
    
    public void Save(SaveData saveData)
    {
        // Items are tracked by SaveManager runtime tracker - nothing to do here
    }
    
    public void Load(SaveData saveData)
    {
        
        // Check with SaveManager if this item was picked up
        if (SaveManager.instance != null)
        {
            bool wasPickedUp = SaveManager.instance.IsItemPickedUp(uniqueID.ID);
            
            if (wasPickedUp)
            {
                Destroy(gameObject);
            }
        }
        else
        {
            Debug.LogWarning($"SaveManager.instance is null for {gameObject.name}");
        }
    }
}
