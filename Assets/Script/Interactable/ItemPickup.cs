using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.Events;

public class ItemPickup : Interactable
{
    public Item itemToPickup;
    public int itemPickupQuantity = 1;

    public override void Interacted()
    {
        base.Interacted();

        //Play Item Pickup Sound
        if (InteractionHandler.instance)
            InteractionHandler.instance.PlayItemPickupSound();

        //Add item to inventory
        if (itemToPickup != null)
            Player.instance.inventory.AddItem(itemToPickup, itemPickupQuantity);
    }
}
