using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemPickupAddon : MonoBehaviour
{
    public Item itemToPickup;

    public void Pickup()
    {
        //Play Item Pickup Sound
        if (InteractionHandler.instance)
            InteractionHandler.instance.PlayItemPickupSound();

        //Add item to inventory
        if (itemToPickup != null)
            Player.instance.inventory.AddItem(itemToPickup);
    }
}
