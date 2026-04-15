using UnityEngine;

/// <summary>
/// Makes a one-time interactable saveable - tracks if it was used
/// Add this component to interactables like DigSpot, PuzzleButton, etc.
/// Also needs UniqueID component
/// 
/// This is different from:
/// - SaveableContainer: Requires LootContainer component
/// - SaveableSwitch: For switches that stay visually activated
/// - SaveableTrigger: For auto-trigger zones
/// </summary>
[RequireComponent(typeof(UniqueID))]
public class SaveableInteractable : MonoBehaviour, ISaveable
{
    private UniqueID uniqueID;
    
    private void Awake()
    {
        uniqueID = GetComponent<UniqueID>();
    }
    
    /// <summary>
    /// Call this when the interactable is used
    /// </summary>
    public void MarkAsUsed()
    {
        Debug.Log($"Interactable used: {gameObject.name} ({uniqueID.ID})");
        
        // Immediately register with SaveManager
        if (SaveManager.instance != null)
        {
            SaveManager.instance.RegisterUsedInteractable(uniqueID.ID);
        }
    }
    
    /// <summary>
    /// Check if this interactable was already used
    /// </summary>
    public bool WasAlreadyUsed()
    {
        if (SaveManager.instance != null)
        {
            return SaveManager.instance.IsInteractableUsed(uniqueID.ID);
        }
        return false;
    }
    
    public void Save(SaveData saveData)
    {
        // Interactables are tracked by SaveManager runtime tracker - nothing to do here
    }
    
    public void Load(SaveData saveData)
    {
        // The interactable script itself should check WasAlreadyUsed() in its Start()
        // We don't auto-disable anything here since each interactable handles state differently
    }
}
