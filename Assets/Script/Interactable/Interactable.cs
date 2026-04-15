using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    public InteractionType interactionType;
    public Transform interactPoint;
    public bool destroyOnInteract;
    public UnityEvent OnInteracted;

    public virtual void Interacted()
    {
        //Interacted by the player
    }

    public virtual void OnTriggerEnter(Collider other)
    {
        //Player Enter Inspectable
        if (other.gameObject.CompareTag("Player"))
        {
            InteractionHandler.instance.InspectableItemTriggerEnter(this);
        }
    }

    public virtual void OnTriggerExit(Collider other)
    {
        //Player Exit Inspectable
        if (other.gameObject.CompareTag("Player"))
        {
            InteractionHandler.instance.InspectableItemTriggerExit(this);
        }
    }
}

[System.Serializable]
public enum InteractionType
{
    Interact,
    Pickup
}
