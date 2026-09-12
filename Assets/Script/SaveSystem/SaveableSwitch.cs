using UnityEngine;

/// <summary>
/// Makes a switch/button/lever saveable - tracks if it was activated
/// Add this component to switch/button GameObjects
/// Also needs UniqueID component
/// </summary>
[RequireComponent(typeof(UniqueID))]
public class SaveableSwitch : MonoBehaviour, ISaveable
{
    private UniqueID uniqueID;
    
    [Header("Optional References")]
    [Tooltip("Optional Animator to trigger when restoring state")]
    [SerializeField] private Animator animator;
    
    [Tooltip("Animation trigger/bool parameter name for activated state")]
    [SerializeField] private string activatedParameterName = "Activated";
    
    [Tooltip("Is this an animation trigger (true) or bool (false)?")]
    [SerializeField] private bool isAnimationTrigger = false;
    
    [Header("Visual State")]
    [Tooltip("Optional GameObject to enable when activated")]
    [SerializeField] private GameObject activatedVisual;
    
    [Tooltip("Optional GameObject to disable when activated")]
    [SerializeField] private GameObject deactivatedVisual;
    
    private void Awake()
    {
        uniqueID = GetComponent<UniqueID>();
    }

    private void Start()
    {
        // Auto-restore if already activated (handles checkpoint loading)
        if (SaveManager.instance != null && SaveManager.instance.IsSwitchActivated(uniqueID.ID))
        {
            RestoreActivatedState();
        }
    }
    
    /// <summary>
    /// Call this when the switch/button is activated
    /// </summary>
    public void MarkAsActivated()
    {
        
        // Immediately register with SaveManager
        if (SaveManager.instance != null)
        {
            SaveManager.instance.RegisterActivatedSwitch(uniqueID.ID);
        }
    }
    
    public void Save(SaveData saveData)
    {
        // Switches are tracked by SaveManager runtime tracker - nothing to do here
    }
    
    public void Load(SaveData saveData)
    {
        
        // Check with SaveManager if this switch was activated
        if (SaveManager.instance != null)
        {
            bool wasActivated = SaveManager.instance.IsSwitchActivated(uniqueID.ID);
            
            if (wasActivated)
            {
                RestoreActivatedState();
            }
        }
        else
        {
            Debug.LogWarning($"SaveManager.instance is null for switch {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Restore the switch to its activated visual state without triggering events
    /// </summary>
    private void RestoreActivatedState()
    {
        // Update animator if present
        if (animator != null && !string.IsNullOrEmpty(activatedParameterName))
        {
            if (isAnimationTrigger)
            {
                animator.SetTrigger(activatedParameterName);
            }
            else
            {
                animator.SetBool(activatedParameterName, true);
            }
        }
        
        // Update visual states
        if (activatedVisual != null)
        {
            activatedVisual.SetActive(true);
        }
        
        if (deactivatedVisual != null)
        {
            deactivatedVisual.SetActive(false);
        }
        
        // Disable interaction if this has a collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }
    }
}
