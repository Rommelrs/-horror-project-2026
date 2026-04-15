using UnityEngine;

public class DoorLockTrigger : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private DoorInteractable door;

    [Header("Required Item")]
    [SerializeField] private Item requiredItem;

    [Header("Activate On Enter")]
    [SerializeField] private GameObject[] objectsToActivate;

    [Header("Settings")]
    [SerializeField] private bool lockOnEnter = true;
    [SerializeField] private bool unlockOnExit = false;
    [SerializeField] private bool destroyAfterTrigger = false;
    
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

        if (lockOnEnter)
        {
            hasTriggered = true;
            
            // Mark as triggered in save system
            if (saveableTrigger != null)
            {
                saveableTrigger.MarkAsTriggered();
            }
            
            LockDoor();
            ActivateObjects();

            if (destroyAfterTrigger)
                Destroy(gameObject);
        }
    }

    private void ActivateObjects()
    {
        if (objectsToActivate == null) return;

        foreach (var obj in objectsToActivate)
        {
            if (obj != null)
                obj.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (unlockOnExit)
        {
            UnlockDoor();
        }
    }

    private void LockDoor()
    {
        if (door != null)
        {
            door.hasDoorLock = true;
        }
    }

    private void UnlockDoor()
    {
        if (door != null)
        {
            door.hasDoorLock = false;
        }
    }

    public void ForceUnlock()
    {
        UnlockDoor();
    }

    public void ForceLock()
    {
        LockDoor();
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
