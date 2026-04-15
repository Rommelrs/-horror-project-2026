using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class InspectableItemPickup : MonoBehaviour
{
    //public int itemId;
    public Transform interactPoint;
    public Item itemToPickup;
    public int itemQuantity =1;

    public bool destroyOnInteract;
    public UnityEvent OnInteracted;
    public AudioClip pckupClip;

    private void OnTriggerEnter(Collider other)
    {
        //Player Enter Inspectable Item Pickup
        if (other.gameObject.CompareTag("Player"))
        {
            ItemInspectionHandler.instance.InspectableItemTriggerEnter(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        //Player Exit Inspectable Item Pickup
        if (other.gameObject.CompareTag("Player"))
        {
            ItemInspectionHandler.instance.InspectableItemTriggerExit(this);
        }
    }
}
