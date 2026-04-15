using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utility class to check player's current resource state for adaptive loot system
/// </summary>
public static class PlayerResourceChecker
{
    // Health thresholds
    public const float HEALTH_CRITICAL = 0.25f;  // 25%
    public const float HEALTH_LOW = 0.50f;       // 50%
    public const float HEALTH_MEDIUM = 0.75f;    // 75%
    
    // Stability thresholds
    public const float STABILITY_CRITICAL = 0.25f;
    public const float STABILITY_LOW = 0.50f;
    public const float STABILITY_MEDIUM = 0.75f;
    
    // Ammo thresholds
    public const int AMMO_CRITICAL = 5;
    public const int AMMO_LOW = 10;
    public const int AMMO_COMFORTABLE = 20;
    public const int AMMO_ABUNDANT = 30;
    
    /// <summary>
    /// Get player's current health as percentage (0.0 - 1.0)
    /// </summary>
    public static float GetHealthPercentage()
    {
        if (Player.instance == null || Player.instance.health == null)
            return 1.0f; // Assume full health if no player found
        
        int currentHealth = Player.instance.health.GetHealthValue();
        int maxHealth = Player.instance.health.GetMaxHealthValue();
        
        return (float)currentHealth / maxHealth;
    }
    
    /// <summary>
    /// Get player's current stability as percentage (0.0 - 1.0)
    /// </summary>
    public static float GetStabilityPercentage()
    {
        if (Player.instance == null || Player.instance.playerStability == null)
            return 1.0f; // Assume full stability if no player found
        
        int currentStability = Player.instance.playerStability.stability;
        int maxStability = Player.instance.playerStability.maxStability;
        
        return (float)currentStability / maxStability;
    }
    
    /// <summary>
    /// Get total ammo count for a specific item type
    /// </summary>
    public static int GetAmmoCount(Item ammoItem)
    {
        if (Player.instance == null || Player.instance.inventory == null || ammoItem == null)
            return 0;
        
        int totalAmmo = 0;
        Player.instance.inventory.HasWeaponAmmo(ammoItem, out totalAmmo);
        
        return totalAmmo;
    }
    
    /// <summary>
    /// Check if player has a specific item type
    /// </summary>
    public static bool HasItemOfType(ItemType itemType)
    {
        if (Player.instance == null || Player.instance.inventory == null)
            return false;
        
        List<Inventory.ItemStack> items = Player.instance.inventory.GetItems();
        
        foreach (var itemStack in items)
        {
            if (itemStack.item != null && itemStack.item.itemType == itemType)
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Get quantity of a specific item type in inventory
    /// </summary>
    public static int GetItemQuantity(ItemType itemType)
    {
        if (Player.instance == null || Player.instance.inventory == null)
            return 0;
        
        int totalQuantity = 0;
        List<Inventory.ItemStack> items = Player.instance.inventory.GetItems();
        
        foreach (var itemStack in items)
        {
            if (itemStack.item != null && itemStack.item.itemType == itemType)
                totalQuantity += itemStack.quantity;
        }
        
        return totalQuantity;
    }
    
    /// <summary>
    /// Check if player is bleeding (needs bandage)
    /// </summary>
    public static bool IsBleeding()
    {
        if (Player.instance == null || Player.instance.bloodEffectHandler == null)
            return false;
        
        // Check if blood effect handler is active
        return Player.instance.bloodEffectHandler.gameObject.activeInHierarchy;
    }
    
    /// <summary>
    /// Check if player has low health
    /// </summary>
    public static bool HasLowHealth()
    {
        return GetHealthPercentage() <= HEALTH_LOW;
    }
    
    /// <summary>
    /// Check if player has critical health
    /// </summary>
    public static bool HasCriticalHealth()
    {
        return GetHealthPercentage() <= HEALTH_CRITICAL;
    }
    
    /// <summary>
    /// Check if player has low stability
    /// </summary>
    public static bool HasLowStability()
    {
        return GetStabilityPercentage() <= STABILITY_LOW;
    }
    
    /// <summary>
    /// Check if player has critical stability
    /// </summary>
    public static bool HasCriticalStability()
    {
        return GetStabilityPercentage() <= STABILITY_CRITICAL;
    }
    
    /// <summary>
    /// Get total ammo count for ALL ammo items in inventory
    /// </summary>
    public static int GetTotalAmmoCount()
    {
        return GetItemQuantity(ItemType.Ammo);
    }
    
    /// <summary>
    /// Check if player has low ammo (checks total ammo, not per-item)
    /// </summary>
    public static bool HasLowAmmo(Item ammoItem = null)
    {
        return GetTotalAmmoCount() <= AMMO_LOW;
    }
    
    /// <summary>
    /// Check if player has critical ammo (checks total ammo, not per-item)
    /// </summary>
    public static bool HasCriticalAmmo(Item ammoItem = null)
    {
        return GetTotalAmmoCount() <= AMMO_CRITICAL;
    }
    
    /// <summary>
    /// Get player's overall resource state (for debugging/logging)
    /// </summary>
    public static string GetResourceStateDebug()
    {
        return $"Health: {GetHealthPercentage():P0} | " +
               $"Stability: {GetStabilityPercentage():P0} | " +
               $"Total Ammo: {GetTotalAmmoCount()} | " +
               $"Healing Items: {GetItemQuantity(ItemType.Healing)} | " +
               $"Bandages: {GetItemQuantity(ItemType.Bandage)} | " +
               $"Stability Pills: {GetItemQuantity(ItemType.AddStability)}";
    }
}
