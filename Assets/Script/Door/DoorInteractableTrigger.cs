using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorInteractableTrigger : MonoBehaviour
{
    [SerializeField] DoorTriggerType doorTriggerType;
    [SerializeField] DoorInteractable doorInteractable;

    private void OnTriggerEnter(Collider other)
    {
        //On Player Enter Collider
        if (other.gameObject.CompareTag("Player"))
        {
            DoorInteractionHandler.instance.DoorInteractionTriggerEnter(doorInteractable, doorTriggerType);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        //On Player Exit Collider
        if (other.gameObject.CompareTag("Player"))
        {
            DoorInteractionHandler.instance.DoorInteractionTriggerExit(doorInteractable);
        }
    }
}

[System.Serializable]
public enum DoorTriggerType
{
    Enter,
    Exit
}
