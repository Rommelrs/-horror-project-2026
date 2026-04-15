using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles saving and loading player data
/// Attach this to the Player GameObject
/// </summary>
[RequireComponent(typeof(Player))]
public class PlayerSaveHandler : MonoBehaviour, ISaveable
{
    private Player player;
    private Health health;
    private Inventory inventory;
    private PlayerWeaponSystem weaponSystem;
    private PlayerStability playerStability;
    
    private void Awake()
    {
        player = GetComponent<Player>();
        health = player.health;
        inventory = player.inventory;
        weaponSystem = player.playerWeaponSystem;
        playerStability = player.playerStability;
    }
    
    public void Save(SaveData saveData)
    {
        if (saveData == null || saveData.playerData == null)
        {
            Debug.LogError("SaveData or PlayerData is null");
            return;
        }
        
        // Save Transform
        saveData.playerData.position = new Vector3Data(transform.position);
        saveData.playerData.rotation = new Vector3Data(transform.eulerAngles);
        
        // Save Health
        if (health != null)
        {
            saveData.playerData.health = health.GetHealthValue();
            saveData.playerData.maxHealth = health.GetMaxHealthValue();
        }
        
        // Save Stability
        if (playerStability != null)
        {
            saveData.playerData.stability = playerStability.stability;
            saveData.playerData.maxStability = playerStability.maxStability;
        }
        
        // Save Weapon Ammo
        if (weaponSystem != null)
        {
            saveData.playerData.currentAmmo = weaponSystem.currentAmmo;
        }
        
        // Save hasMap flag
        saveData.playerData.hasMap = player.hasMap;
        
        // Save Inventory
        if (inventory != null)
        {
            saveData.playerData.items.Clear();
            saveData.playerData.notes.Clear();
            
            // Save items
            List<Inventory.ItemStack> items = inventory.GetItems();
            
            foreach (var itemStack in items)
            {
                if (itemStack.item != null)
                {
                    saveData.playerData.items.Add(new ItemStackData(itemStack.item.name, itemStack.quantity));
                }
                else
                {
                    Debug.LogWarning("Found null item in inventory during save!");
                }
            }
            
            // Save notes
            List<Inventory.ItemStack> notes = inventory.GetNotes();
            
            foreach (var noteStack in notes)
            {
                if (noteStack.item != null)
                {
                    saveData.playerData.notes.Add(new ItemStackData(noteStack.item.name, noteStack.quantity));
                }
            }
            
        }
        
    }
    
    public void Load(SaveData saveData)
    {
        if (saveData == null || saveData.playerData == null)
        {
            Debug.LogError("SaveData or PlayerData is null");
            return;
        }
        
        // Load Transform
        if (player.controller != null)
        {
            player.controller.enabled = false;
            transform.position = saveData.playerData.position.ToVector3();
            transform.eulerAngles = saveData.playerData.rotation.ToVector3();
            player.controller.enabled = true;
        }
        else
        {
            transform.position = saveData.playerData.position.ToVector3();
            transform.eulerAngles = saveData.playerData.rotation.ToVector3();
        }
        
        // Load Health
        if (health != null)
        {
            health.ResetHealth(); // Reset first
            int healthDiff = saveData.playerData.health - health.GetHealthValue();
            if (healthDiff < 0)
            {
                health.Damage(-healthDiff);
            }
            else if (healthDiff > 0)
            {
                health.Heal(healthDiff);
            }
        }
        
        // Load Stability
        if (playerStability != null)
        {
            playerStability.stability = saveData.playerData.stability;
            playerStability.maxStability = saveData.playerData.maxStability;
        }
        
        // Load Weapon Ammo
        if (weaponSystem != null)
        {
            weaponSystem.currentAmmo = saveData.playerData.currentAmmo;
            // Update ammo UI text if available
            if (weaponSystem.ammoTxt != null)
            {
                weaponSystem.ammoTxt.text = weaponSystem.currentAmmo.ToString();
            }
        }
        
        // Load hasMap flag
        player.hasMap = saveData.playerData.hasMap;
        
        // Load Inventory
        if (inventory != null)
        {
            // Clear current inventory (we'll load fresh from save)
            // Note: You might need to add a ClearInventory method to Inventory.cs
            StartCoroutine(LoadInventoryAfterFrame(saveData.playerData));
        }
        
    }
    
    // Load inventory after a frame to ensure inventory system is ready
    private System.Collections.IEnumerator LoadInventoryAfterFrame(PlayerData playerData)
    {
        yield return new WaitForEndOfFrame();
        
        // IMPORTANT: Clear existing inventory first!
        inventory.ClearInventory();
        
        
        // Load items
        foreach (var itemData in playerData.items)
        {
            // Find item by searching all loaded Item ScriptableObjects
            Item item = FindItemByName(itemData.itemName);
            if (item != null)
            {
                inventory.AddItem(item, itemData.quantity);
            }
            else
            {
                Debug.LogWarning($"Could not find item: {itemData.itemName}");
            }
        }
        
        // Load notes
        foreach (var noteData in playerData.notes)
        {
            Item note = FindItemByName(noteData.itemName);
            if (note != null)
            {
                inventory.AddItem(note, noteData.quantity);
            }
            else
            {
                Debug.LogWarning($"Could not find note: {noteData.itemName}");
            }
        }
    }
    
    // Find an Item ScriptableObject by name from all loaded assets
    private Item FindItemByName(string itemName)
    {
#if UNITY_EDITOR
        // In editor, use AssetDatabase to find items
        string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:Item {itemName}");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            Item item = UnityEditor.AssetDatabase.LoadAssetAtPath<Item>(path);
            if (item != null && item.name == itemName)
            {
                return item;
            }
        }
#endif
        // In build, try Resources.Load with Items path
        Item resourceItem = Resources.Load<Item>($"Items/{itemName}");
        if (resourceItem != null) return resourceItem;
        
        // Also try checking in subfolders (like Items/Supplies)
        Item[] allItems = Resources.LoadAll<Item>("Items");
        foreach (Item item in allItems)
        {
            if (item.name == itemName)
                return item;
        }
        
        return null;
    }
}
