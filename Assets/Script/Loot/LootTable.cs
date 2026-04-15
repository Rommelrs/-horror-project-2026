using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewLootTable", menuName = "Loot/Loot Table")]
public class LootTable : ScriptableObject
{
    [System.Serializable]
    public class LootEntry
    {
        [Tooltip("The prefab to spawn (must have ItemPickup component)")]
        public GameObject itemPrefab;
        
        [Tooltip("Weight/probability of this item dropping (higher = more common)")]
        [Range(0f, 100f)]
        public float dropWeight = 10f;
        
        [Tooltip("Is this a guaranteed drop? (ignores weight system)")]
        public bool isGuaranteed = false;
        
        [Header("Adaptive Settings")]
        [Tooltip("Enable adaptive weight adjustment based on player needs?")]
        public bool useAdaptiveWeight = true;
    }
    
    [Header("Loot Configuration")]
    [Tooltip("List of items that can drop from this loot table")]
    public List<LootEntry> lootEntries = new List<LootEntry>();
    
    [Header("Drop Settings")]
    [Tooltip("Minimum number of items to spawn")]
    [Min(0)]
    public int minDrops = 1;
    
    [Tooltip("Maximum number of items to spawn")]
    [Min(1)]
    public int maxDrops = 1;
    
    [Tooltip("Chance that nothing drops (0-100%)")]
    [Range(0f, 100f)]
    public float emptyChance = 0f;
    
    [Header("Adaptive System")]
    [Tooltip("Enable adaptive loot system for this table?")]
    public bool enableAdaptiveLoot = true;
    
    [Tooltip("Multiplier when player critically needs item (e.g., 0 ammo, <25% health)")]
    [Range(1f, 5f)]
    public float criticalNeedMultiplier = 3.0f;
    
    [Tooltip("Multiplier when player has low resources (e.g., <15 ammo, <50% health)")]
    [Range(1f, 3f)]
    public float lowNeedMultiplier = 2.0f;
    
    [Tooltip("Multiplier when player has plenty of resources")]
    [Range(0.1f, 1f)]
    public float abundanceMultiplier = 0.5f;
    
    [Header("Debug")]
    [Tooltip("Enable debug logging for loot rolls?")]
    public bool enableDebugLogs = false;
    
    /// <summary>
    /// Roll the loot table and return a list of prefabs to spawn
    /// </summary>
    public List<PrefabDrop> RollLoot()
    {
        List<PrefabDrop> drops = new List<PrefabDrop>();
        
        if (enableDebugLogs)
        {
        }
        
        // Check for empty drop
        if (Random.Range(0f, 100f) < emptyChance)
        {
            // Debug log removed
            return drops;
        }
        
        // Add guaranteed items first
        foreach (var entry in lootEntries)
        {
            if (entry.isGuaranteed && entry.itemPrefab != null)
            {
                drops.Add(new PrefabDrop(entry.itemPrefab));
                // Debug log removed
            }
        }
        
        // Roll for random drops
        int dropCount = Random.Range(minDrops, maxDrops + 1);
        // Debug log removed
        
        for (int i = 0; i < dropCount; i++)
        {
            GameObject rolledPrefab = RollSinglePrefab();
            if (rolledPrefab != null)
            {
                drops.Add(new PrefabDrop(rolledPrefab));
                // Debug log removed
            }
        }
        
        // Debug log removed
        return drops;
    }
    
    /// <summary>
    /// Roll for a single prefab based on weights (with adaptive logic)
    /// </summary>
    private GameObject RollSinglePrefab()
    {
        // Calculate total weight with adaptive multipliers
        float totalWeight = 0f;
        
        // Debug log removed
        
        foreach (var entry in lootEntries)
        {
            if (!entry.isGuaranteed && entry.itemPrefab != null && entry.dropWeight > 0)
            {
                float adaptiveWeight = GetAdaptiveWeight(entry);
                totalWeight += adaptiveWeight;
                
                if (enableDebugLogs)
                {
                    ItemPickup pickup = entry.itemPrefab.GetComponent<ItemPickup>();
                    InspectableItemPickup inspectable = entry.itemPrefab.GetComponent<InspectableItemPickup>();
                    Item itemRef = pickup?.itemToPickup ?? inspectable?.itemToPickup;
                    string itemName = itemRef != null ? itemRef.itemName : entry.itemPrefab.name;
                    float multiplier = adaptiveWeight / entry.dropWeight;
                }
            }
        }
        
        if (totalWeight <= 0)
        {
            // Debug log removed
            return null;
        }
        
        // Debug log removed
        
        // Roll random value
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;
        
        // Debug log removed
        
        // Find which prefab was rolled
        foreach (var entry in lootEntries)
        {
            if (entry.isGuaranteed || entry.itemPrefab == null || entry.dropWeight <= 0)
                continue;
            
            float adaptiveWeight = GetAdaptiveWeight(entry);
            currentWeight += adaptiveWeight;
            
            if (randomValue <= currentWeight)
            {
                // Debug log removed
                return entry.itemPrefab;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Calculate adaptive weight based on player's current resource state
    /// </summary>
    private float GetAdaptiveWeight(LootEntry entry)
    {
        if (!enableAdaptiveLoot || !entry.useAdaptiveWeight || entry.itemPrefab == null)
        {
            // Debug log removed
            return entry.dropWeight;
        }
        
        // Get the ItemPickup component to check item type (try both ItemPickup and InspectableItemPickup)
        ItemPickup itemPickup = entry.itemPrefab.GetComponent<ItemPickup>();
        InspectableItemPickup inspectablePickup = entry.itemPrefab.GetComponent<InspectableItemPickup>();
        
        Item item = null;
        if (itemPickup != null)
            item = itemPickup.itemToPickup;
        else if (inspectablePickup != null)
            item = inspectablePickup.itemToPickup;
        
        if (item == null)
        {
            // Debug log removed
            return entry.dropWeight;
        }
        
        float multiplier = 1.0f;
        ItemType itemType = item.itemType;
        
        if (enableDebugLogs)
        {
        }
        
        // Check item type and apply adaptive multiplier based on player needs
        switch (itemType)
        {
            case ItemType.Healing:
            case ItemType.HealingAndAddStability:
                multiplier = GetHealthBasedMultiplier();
                break;
                
            case ItemType.AddStability:
            case ItemType.CalmingInhaler:
                multiplier = GetStabilityBasedMultiplier();
                break;
                
            case ItemType.Ammo:
                multiplier = GetAmmoBasedMultiplier();
                break;
                
            case ItemType.Bandage:
                multiplier = GetBandageBasedMultiplier();
                break;
                
            case ItemType.EnergyDrink:
                // Energy drinks are less critical, slight boost if player has low stability
                if (PlayerResourceChecker.HasLowStability())
                    multiplier = 1.5f;
                break;
                
            // Key items, tools, and other non-consumables don't use adaptive logic
            case ItemType.Key:
            case ItemType.Drill:
            case ItemType.Fuse:
            case ItemType.Note:
            case ItemType.Weapon:
                multiplier = 1.0f; // No adaptive logic for these
                break;
        }
        
        return entry.dropWeight * multiplier;
    }
    
    /// <summary>
    /// Get multiplier based on player's health state AND healing item inventory
    /// </summary>
    private float GetHealthBasedMultiplier()
    {
        float healthPercent = PlayerResourceChecker.GetHealthPercentage();
        int healingItemCount = PlayerResourceChecker.GetItemQuantity(ItemType.Healing) + 
                               PlayerResourceChecker.GetItemQuantity(ItemType.HealingAndAddStability);
        
        // Critical need: Low health + no healing items
        if (healthPercent <= PlayerResourceChecker.HEALTH_CRITICAL && healingItemCount == 0)
            return criticalNeedMultiplier;
        
        // Low health but has healing items - reduce boost
        if (healthPercent <= PlayerResourceChecker.HEALTH_LOW && healingItemCount > 0)
            return 1.0f; // Normal rate - player has items to heal
        
        // Low health, no items
        if (healthPercent <= PlayerResourceChecker.HEALTH_LOW && healingItemCount == 0)
            return lowNeedMultiplier;
        
        // Full/high health but no healing items - slight boost for preparedness
        if (healthPercent >= 0.9f && healingItemCount == 0)
            return 1.0f; // Normal rate
        
        // Has 3+ healing items - reduce drops significantly
        if (healingItemCount >= 3)
            return abundanceMultiplier * 0.5f; // Even more reduced
        
        // Has some healing items
        if (healingItemCount >= 1)
            return abundanceMultiplier;
        
        return 1.0f; // Normal drop rate
    }
    
    /// <summary>
    /// Get multiplier based on player's stability state AND stability item inventory
    /// </summary>
    private float GetStabilityBasedMultiplier()
    {
        float stabilityPercent = PlayerResourceChecker.GetStabilityPercentage();
        int stabilityItemCount = PlayerResourceChecker.GetItemQuantity(ItemType.AddStability) + 
                                 PlayerResourceChecker.GetItemQuantity(ItemType.CalmingInhaler);
        
        // Critical need: Low stability + no items
        if (stabilityPercent <= PlayerResourceChecker.STABILITY_CRITICAL && stabilityItemCount == 0)
            return criticalNeedMultiplier;
        
        // Low stability but has items
        if (stabilityPercent <= PlayerResourceChecker.STABILITY_LOW && stabilityItemCount > 0)
            return 1.0f; // Normal - player has items
        
        // Low stability, no items
        if (stabilityPercent <= PlayerResourceChecker.STABILITY_LOW && stabilityItemCount == 0)
            return lowNeedMultiplier;
        
        // Has 3+ stability items
        if (stabilityItemCount >= 3)
            return abundanceMultiplier * 0.5f;
        
        // Has some stability items
        if (stabilityItemCount >= 1)
            return abundanceMultiplier;
        
        return 1.0f;
    }
    
    /// <summary>
    /// Get multiplier based on player's total ammo state (already inventory-based)
    /// </summary>
    private float GetAmmoBasedMultiplier()
    {
        int totalAmmo = PlayerResourceChecker.GetTotalAmmoCount();
        
        if (enableDebugLogs)
        {
        }
        
        // Critical need (0-5 bullets)
        if (totalAmmo <= PlayerResourceChecker.AMMO_CRITICAL)
        {
            // Debug log removed
            return criticalNeedMultiplier;
        }
        // Low ammo (6-10 bullets)
        else if (totalAmmo <= PlayerResourceChecker.AMMO_LOW)
        {
            // Debug log removed
            return lowNeedMultiplier;
        }
        // Comfortable range (11-19 bullets) - start reducing
        else if (totalAmmo < PlayerResourceChecker.AMMO_COMFORTABLE)
        {
            // Debug log removed
            return abundanceMultiplier; // 0.5x
        }
        // Getting stocked (20-29 bullets) - reduce more
        else if (totalAmmo < PlayerResourceChecker.AMMO_ABUNDANT)
        {
            float heavyReduction = abundanceMultiplier * 0.5f; // 0.25x
            // Debug log removed
            return heavyReduction;
        }
        // Abundant (30+ bullets) - extremely rare
        else
        {
            float extremeReduction = abundanceMultiplier * 0.2f; // 0.1x
            // Debug log removed
            return extremeReduction;
        }
    }
    
    /// <summary>
    /// Get multiplier based on player's bleeding/bandage needs
    /// </summary>
    private float GetBandageBasedMultiplier()
    {
        // If player is bleeding and has no bandages, critical need
        if (PlayerResourceChecker.IsBleeding() && !PlayerResourceChecker.HasItemOfType(ItemType.Bandage))
            return criticalNeedMultiplier;
        
        // If player is bleeding but has bandages, still boost slightly
        if (PlayerResourceChecker.IsBleeding())
            return lowNeedMultiplier;
        
        // If player has multiple bandages and not bleeding, reduce drops
        if (PlayerResourceChecker.GetItemQuantity(ItemType.Bandage) >= 3)
            return abundanceMultiplier;
        
        return 1.0f;
    }
    
}

/// <summary>
/// Represents a single prefab drop result
/// </summary>
[System.Serializable]
public class PrefabDrop
{
    public GameObject prefab;
    
    public PrefabDrop(GameObject prefab)
    {
        this.prefab = prefab;
    }
}
