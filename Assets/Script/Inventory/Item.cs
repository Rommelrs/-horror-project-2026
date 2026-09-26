using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class Item : ScriptableObject
{
    [Header("Item Details")]
    public string itemName; // Name of the item
    public Sprite itemIcon; // Icon for the item
    public string description; // Description of the item

    [Header("Item Properties")]
    public bool isStackable;
    public int maxStackSize = 1;

    [Header("Item Function")]
    public ItemType itemType;
    [Tooltip("Notes show a 'Press [Read] to read' hint by default. Turn off only for notes with no actual text content (e.g. a drawing meant to just be looked at), since the read prompt would otherwise show and reveal nothing.")]
    public bool showReadPrompt = true;
    public int healingAmount;
    public int stabilityIncreaseAmount;
    public int energyDrinkDuration;
    public int calmingInhalerDuration;
    public int keyCode;
}

[System.Serializable]
public enum ItemType
{
    Regular,
    Healing,
    AddStability,
    HealingAndAddStability,
    Weapon,
    EnergyDrink,
    Key,
    Ammo,
    Bandage,
    CalmingInhaler,
    Drill,
    DrillCharge,
    Fuse,
    Note,
    Shovel,
    Lighter,
    UVLight,
    DuctTape,
    WoodenPlank,
    Knife,
    Map1,
    Map2
}
