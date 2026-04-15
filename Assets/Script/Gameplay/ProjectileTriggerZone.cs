using UnityEngine;

public class ProjectileTriggerZone : MonoBehaviour
{
    [Header("Required Item")]
    [SerializeField] private Item requiredItem;

    [Header("Projectile Throwers")]
    [SerializeField] private ProjectileThrower[] projectileThrowers;

    [Header("Settings")]
    [SerializeField] private bool disableOnExit = true;
    [SerializeField] private bool destroyTriggerAfterActivation = false;

    private bool hasActivated = false;

    private void Start()
    {
        // Make sure all throwers are disabled at start
        foreach (var thrower in projectileThrowers)
        {
            if (thrower != null)
                thrower.enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        // Check save system
        SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
        if (saveableTrigger != null && saveableTrigger.WasAlreadyTriggered())
        {
            hasActivated = true;
            return;
        }

        bool hasItem = PlayerHasItem();

        if (hasItem)
        {
            ActivateThrowers();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (disableOnExit && !destroyTriggerAfterActivation)
        {
            DeactivateThrowers();
        }
    }

    private bool PlayerHasItem()
    {
        if (requiredItem == null) return true; // No item required

        if (Player.instance == null || Player.instance.inventory == null) return false;

        foreach (var stack in Player.instance.inventory.GetItems())
        {
            if (stack.item == requiredItem) return true;
        }
        return false;
    }

    private void ActivateThrowers()
    {
        hasActivated = true;
        
        // Mark as triggered in save system
        SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
        if (saveableTrigger != null)
        {
            saveableTrigger.MarkAsTriggered();
        }

        foreach (var thrower in projectileThrowers)
        {
            if (thrower != null)
            {
                thrower.enabled = true;
            }
        }

        if (destroyTriggerAfterActivation)
            Destroy(gameObject);
    }

    private void DeactivateThrowers()
    {
        foreach (var thrower in projectileThrowers)
        {
            if (thrower != null)
                thrower.enabled = false;
        }
    }

    public void StopThrowers()
    {
        DeactivateThrowers();
    }
}
