using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorInteractableTrigger : MonoBehaviour
{
    [SerializeField] DoorTriggerType doorTriggerType;
    [SerializeField] DoorInteractable doorInteractable;

    // 'Auto Interact On Enter' doors: used once per visit to the trigger
    bool autoUsedThisVisit = false;

    private void OnTriggerEnter(Collider other)
    {
        //On Player Enter Collider
        if (other.gameObject.CompareTag("Player"))
        {
            DoorInteractionHandler.instance.DoorInteractionTriggerEnter(doorInteractable, doorTriggerType);
            autoUsedThisVisit = false;
            TryAutoInteract();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // if the door could not be used the moment the player walked in (paused, inspecting an item, another door still running),
        // use it as soon as it can be
        if (!autoUsedThisVisit && other.gameObject.CompareTag("Player"))
            TryAutoInteract();
    }

    private void OnTriggerExit(Collider other)
    {
        //On Player Exit Collider
        if (other.gameObject.CompareTag("Player"))
        {
            DoorInteractionHandler.instance.DoorInteractionTriggerExit(doorInteractable);
            autoUsedThisVisit = false;
        }
    }

    void TryAutoInteract()
    {
        if (doorInteractable == null || !doorInteractable.autoInteractOnEnter) return;
        var handler = DoorInteractionHandler.instance;
        if (handler == null || !handler.CanAutoInteract()) return;
        autoUsedThisVisit = true;
        handler.DoorInteractionTriggerEnter(doorInteractable, doorTriggerType);   // make sure this door/side is the current one
        handler.InteractWithCurrentDoor();
    }
}

[System.Serializable]
public enum DoorTriggerType
{
    Enter,
    Exit
}
