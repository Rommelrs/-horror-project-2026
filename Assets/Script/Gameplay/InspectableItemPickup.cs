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
    [Tooltip("Pick it straight up: no inspection screen and no 'I got a ...' subtitle from this pickup's own SubtitleTrigger.")]
    public bool instantPickup;
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
