using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Inventory : MonoBehaviour
{
    [System.Serializable]
    public struct ItemStack
    {
        public Item item;
        public int quantity;
        public ItemStack(Item item, int quantity)
        {
            this.item = item;
            this.quantity = quantity;
        }
    }

    [Header("Inventory Settings")]
    [SerializeField] private int maxInventorySize = 20;
    [SerializeField] private List<ItemStack> defaultItems = new List<ItemStack>();
    [SerializeField] AudioClip pickupSound;
    
    [Header("Item Combining")]
    [SerializeField] Item knifeHandleItem;
    [SerializeField] Item knifeBladeItem;
    [SerializeField] Item combinedKnifeItem;
    [SerializeField] SubtitleTrigger knifeCombinedSubtitleTrigger;

    //Inventory items - use static lists to persist across scenes
    private static List<ItemStack> _persistentItems = new List<ItemStack>();
    private static List<ItemStack> _persistentNotes = new List<ItemStack>();
    private static bool _hasInitialized = false;

    //Local references that sync with static data
    [SerializeField] List<ItemStack> items = new List<ItemStack>();
    [SerializeField] List<ItemStack> notes = new List<ItemStack>();

    //Event to notify when the inventory is updated
    public UnityEvent OnInventoryItemUpdated;

    AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        
        // Load persistent data
        if (_hasInitialized)
        {
            items = new List<ItemStack>(_persistentItems);
            notes = new List<ItemStack>(_persistentNotes);
        }
    }

    private IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();
        
        // Only initialize default items once across all scenes
        if (!_hasInitialized)
        {
            InitializeDefaultItems();
            _hasInitialized = true;
            SaveToPersistent();
        }
    }

    // Save current inventory to static persistent storage
    private void SaveToPersistent()
    {
        _persistentItems = new List<ItemStack>(items);
        _persistentNotes = new List<ItemStack>(notes);
    }

    // Initialize the inventory with default items
    private void InitializeDefaultItems()
    {
        foreach (var defaultItem in defaultItems)
        {
            AddItem(defaultItem.item, defaultItem.quantity);
        }
    }

    // Add an item to the inventory
    public bool AddItem(Item item, int quantity = 1)
    {
        if (item == null || quantity <= 0)
        {
            Debug.LogWarning("Invalid item or quantity.");
            return false;
        }

        if(item.itemType == ItemType.Note)
        {
            notes.Add(new ItemStack(item, item.maxStackSize));
            SaveToPersistent();
            OnInventoryItemUpdated?.Invoke();
            return true;
        }

        // Check if the item is stackable and already exists in the inventory
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].item == item && item.isStackable)
            {
                if (items[i].quantity >= item.maxStackSize)
                    continue;

                // Add to the existing stack, but ensure it doesn't exceed the max stack size
                int newQuantity = items[i].quantity + quantity;
                if (newQuantity > item.maxStackSize)
                {
                    int excess = newQuantity - item.maxStackSize;
                    items[i] = new ItemStack(item, item.maxStackSize);
                    return AddItem(item, excess); // Add the excess as a new stack
                }
                else
                {
                    items[i] = new ItemStack(item, newQuantity);
                    SaveToPersistent();
                    OnInventoryItemUpdated?.Invoke();
                    return true;
                }
            }
        }

        // If the item is not stackable or doesn't exist in the inventory, add a new stack
        if (items.Count < maxInventorySize)
        {
            int quantityToAdd = Mathf.Min(quantity, item.maxStackSize);
            items.Add(new ItemStack(item, quantityToAdd));

            // If there's excess quantity, try adding it as a new stack
            if (quantity > item.maxStackSize)
            {
                return AddItem(item, quantity - item.maxStackSize);
            }

            SaveToPersistent();
            OnInventoryItemUpdated?.Invoke();
            
            // Check for knife combining after adding item
            CheckKnifeCombining();
            
            return true;
        }

        Debug.LogWarning("Inventory is full! Cannot add item: " + item.itemName);
        return false;
    }
    
    private void CheckKnifeCombining()
    {
        if (knifeHandleItem == null || knifeBladeItem == null || combinedKnifeItem == null)
            return;
        
        bool hasHandle = HasItem(knifeHandleItem);
        bool hasBlade = HasItem(knifeBladeItem);
        bool hasFullKnife = HasItem(combinedKnifeItem);
        
        // If has both parts but not the combined knife, combine them
        if (hasHandle && hasBlade && !hasFullKnife)
        {
            RemoveItem(knifeHandleItem, 1);
            RemoveItem(knifeBladeItem, 1);
            
            // Add combined knife without triggering another combine check
            if (items.Count < maxInventorySize)
            {
                items.Add(new ItemStack(combinedKnifeItem, 1));
                SaveToPersistent();
                OnInventoryItemUpdated?.Invoke();
            }
            
            // Trigger subtitle when parts combine (with delay to avoid overlap)
            if (knifeCombinedSubtitleTrigger != null)
                StartCoroutine(TriggerSubtitleAfterDelay(knifeCombinedSubtitleTrigger, 0.5f));
        }
        // If has full knife, split it into parts
        else if (hasFullKnife && !hasHandle && !hasBlade)
        {
            // This case shouldn't happen naturally, but handle it if knife is somehow in inventory
            // We'll only auto-combine, not auto-split
        }
    }

    // Remove an item from the inventory
    public bool RemoveItem(Item item, int quantity = 1)
    {
        if (item == null || quantity <= 0)
        {
            Debug.LogWarning("Invalid item or quantity.");
            return false;
        }

        if (item.itemType == ItemType.Note)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                if (notes[i].item == item)
                {
                    notes.RemoveAt(i);
                    SaveToPersistent();
                    return true;
                }
            }
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].item == item)
            {
                if (items[i].quantity > quantity)
                {
                    // Reduce the quantity in the stack
                    items[i] = new ItemStack(item, items[i].quantity - quantity);
                    SaveToPersistent();
                    OnInventoryItemUpdated?.Invoke();
                    return true;
                }
                else if (items[i].quantity == quantity)
                {
                    // Remove the stack entirely
                    items.RemoveAt(i);
                    SaveToPersistent();
                    OnInventoryItemUpdated?.Invoke();
                    return true;
                }
                else
                {
                    // Not enough quantity to remove
                    Debug.LogWarning("Not enough quantity to remove: " + item.itemName);
                    return false;
                }
            }
        }

        Debug.LogWarning("Item not found in inventory: " + item.itemName);
        return false;
    }

    // Check if inventory has a specific item
    public bool HasItem(Item item)
    {
        if (item == null)
            return false;
        
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].item == item)
                return true;
        }
        
        return false;
    }
    
    private IEnumerator TriggerSubtitleAfterDelay(SubtitleTrigger trigger, float delay)
    {
        yield return new WaitForSeconds(delay);
        trigger.TriggerSubtitle();
    }
    
    // Play the pickup sound
    public void PlayPickupSound()
    {
        if (audioSource != null && pickupSound != null)
        {
            audioSource.PlayOneShot(pickupSound);
        }
    }

    public bool HasWeapon()
    {
        List<ItemStack> items = GetItems();
        if (items != null && items.Count > 0)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].item.itemType == ItemType.Weapon)
                    return true;
            }
        }

        return false;
    }

    public bool HasFuse()
    {
        List<ItemStack> items = GetItems();
        if (items != null && items.Count > 0)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].item.itemType == ItemType.Fuse)
                    return true;
            }
        }

        return false;
    }

    public bool HasWeaponAmmo(Item itemReference, out int ammoCount)
    {
        ammoCount = 0;

        List<ItemStack> items = GetItems();
        if (items != null && items.Count > 0)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].item.itemType == ItemType.Ammo && items[i].item.itemName == itemReference.itemName)
                {
                    ammoCount += items[i].quantity;
                }
            }
        }

        if (ammoCount > 0)
            return true;
        else
            return false;
    }

    public bool HasDrill()
    {
        List<ItemStack> items = GetItems();
        if (items != null && items.Count > 0)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].item.itemType == ItemType.Drill)
                    return true;
            }
        }

        return false;
    }

    public int GetDrillChargeCount()
    {
        int drillCharge = 0;

        List<ItemStack> items = GetItems();
        if (items != null && items.Count > 0)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].item.itemType == ItemType.DrillCharge)
                {
                    drillCharge += items[i].quantity;
                }
            }
        }

        return drillCharge;
    }

    public void RemoveDrillCharge()
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].item.itemType == ItemType.DrillCharge)
            {
                RemoveItem(items[i].item);
                return;
            }
        }
    }

    // Get the current inventory size
    public List<ItemStack> GetItems()
    {
        return new List<ItemStack>(items);
    }

    public List<ItemStack> GetNotes()
    {
        return new List<ItemStack>(notes);
    }
    
    /// <summary>
    /// Clear all items and notes from inventory (for new game or save/load)
    /// </summary>
    public void ClearInventory()
    {
        items.Clear();
        notes.Clear();
        SaveToPersistent();
        OnInventoryItemUpdated?.Invoke();
        
        // Reset initialization flag so default items can be added again on next scene
        _hasInitialized = false;
    }
}
