using UnityEngine;

/// <summary>
/// Makes a one-time trigger zone saveable - tracks if it was already triggered
/// Add this component to trigger area GameObjects (like ElectricityTrapController, OnPlayerTrigger)
/// Also needs UniqueID component
/// 
/// This is different from SaveableSwitch:
/// - SaveableSwitch: For interactive switches/buttons that get activated
/// - SaveableTrigger: For one-time trigger zones that auto-activate when player enters
/// 
/// USAGE:
/// Option 1: Manual - Call MarkAsTriggered() and WasAlreadyTriggered() in your script
/// Option 2: Auto - Set Auto Restore to true and it will disable the trigger collider after load
/// </summary>
[RequireComponent(typeof(UniqueID))]
public class SaveableTrigger : MonoBehaviour, ISaveable
{
    private UniqueID uniqueID;
    
    [Header("Auto Restore Settings")]
    [Tooltip("If true, automatically disable the trigger collider if already triggered")]
    [SerializeField] private bool autoRestore = true;
    
    [Tooltip("If true, also disable the entire GameObject if already triggered")]
    [SerializeField] private bool disableGameObject = false;
    
    private void Awake()
    {
        uniqueID = GetComponent<UniqueID>();
    }

    private void Start()
    {
        // Auto-disable if already triggered (handles checkpoint/scene transition loading)
        if (SaveManager.instance != null && SaveManager.instance.IsZoneTriggered(uniqueID.ID))
        {
            if (disableGameObject)
                gameObject.SetActive(false);
            else
            {
                Collider col = GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
        }
    }
    
    /// <summary>
    /// Call this when the trigger is activated
    /// </summary>
    public void MarkAsTriggered()
    {
        
        // Immediately register with SaveManager
        if (SaveManager.instance != null)
        {
            SaveManager.instance.RegisterTriggeredZone(uniqueID.ID);
        }
    }
    
    /// <summary>
    /// Check if this trigger was already activated
    /// Use this in your trigger logic to prevent re-triggering
    /// </summary>
    public bool WasAlreadyTriggered()
    {
        if (SaveManager.instance != null)
        {
            return SaveManager.instance.IsZoneTriggered(uniqueID.ID);
        }
        return false;
    }
    
    public void Save(SaveData saveData)
    {
        // Triggers are tracked by SaveManager runtime tracker - nothing to do here
    }
    
    public void Load(SaveData saveData)
    {
        
        // Check with SaveManager if this trigger was already activated
        if (SaveManager.instance != null)
        {
            bool wasTriggered = SaveManager.instance.IsZoneTriggered(uniqueID.ID);
            
            if (wasTriggered && autoRestore)
            {
                
                if (disableGameObject)
                {
                    // Disable entire GameObject
                    gameObject.SetActive(false);
                }
                else
                {
                    // Just disable the collider to prevent re-triggering
                    Collider col = GetComponent<Collider>();
                    if (col != null)
                    {
                        col.enabled = false;
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning($"SaveManager.instance is null for trigger {gameObject.name}");
        }
    }
}
