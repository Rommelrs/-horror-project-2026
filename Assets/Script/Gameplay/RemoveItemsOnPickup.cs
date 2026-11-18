using UnityEngine;

/// <summary>
/// PROTOTYPE (Block 2 wardrobe): the moment 'watchItem' arrives in the inventory, 'itemsToRemove' leave it (the duct tape
/// picked up -> he leaves the gate key behind). No animation. Only on a real pickup, not when a save already has the item.
/// </summary>
public class RemoveItemsOnPickup : MonoBehaviour
{
    [SerializeField] Item watchItem;
    [SerializeField] Item[] itemsToRemove = new Item[0];

    bool hadItem, done;
    Inventory inventory;

    void Start()
    {
        if (Player.instance == null || Player.instance.inventory == null || watchItem == null) return;
        inventory = Player.instance.inventory;
        hadItem = inventory.HasItem(watchItem);
        inventory.OnInventoryItemUpdated.AddListener(OnInventoryUpdated);
    }

    void OnDestroy()
    {
        if (inventory != null) inventory.OnInventoryItemUpdated.RemoveListener(OnInventoryUpdated);
    }

    void OnInventoryUpdated()
    {
        if (done || inventory == null) return;
        bool has = inventory.HasItem(watchItem);
        if (has && !hadItem && !CheckpointManager.IsLoadingCheckpoint)
        {
            done = true;
            foreach (var item in itemsToRemove)
                if (item != null)
                    while (inventory.HasItem(item)) if (!inventory.RemoveItem(item)) break;
        }
        hadItem = has;
    }
}
