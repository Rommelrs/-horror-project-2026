using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Triggers action when specified GameObjects are destroyed or disabled
/// Useful for: "when player picks up 2 items" (items get destroyed on pickup)
/// </summary>
public class ObjectDisappearanceDetector : MonoBehaviour
{
    [Header("Objects to Monitor")]
    [Tooltip("GameObjects to check - when ALL are gone/disabled, trigger action")]
    public GameObject[] objectsToMonitor;
    
    [Header("Actions")]
    [Tooltip("GameObjects to enable when all monitored objects are gone")]
    public GameObject[] objectsToEnable;
    
    [Tooltip("Event triggered when all objects disappear")]
    public UnityEvent onAllObjectsGone;
    
    private bool hasTriggered = false;
    
    private void Update()
    {
        if (hasTriggered)
            return;
        
        // Check if all objects are gone (null or inactive)
        if (AllObjectsAreGone())
        {
            TriggerAction();
            hasTriggered = true;
            enabled = false; // Stop checking
        }
    }
    
    private bool AllObjectsAreGone()
    {
        foreach (GameObject obj in objectsToMonitor)
        {
            // If object still exists and is active, not all gone yet
            if (obj != null && obj.activeInHierarchy)
            {
                return false;
            }
        }
        
        // All objects are either null (destroyed) or inactive
        return true;
    }
    
    private void TriggerAction()
    {
        Debug.Log("[ObjectDisappearanceDetector] All monitored objects are gone!");
        
        // Enable objects
        foreach (GameObject obj in objectsToEnable)
        {
            if (obj != null)
            {
                obj.SetActive(true);
            }
        }
        
        // Trigger event
        onAllObjectsGone?.Invoke();
    }
}
