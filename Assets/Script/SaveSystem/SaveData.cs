using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main save data container - this is what gets serialized to JSON
/// </summary>
[System.Serializable]
public class SaveData
{
    public string saveFileName;
    public string sceneName;
    public DateTime saveDate;
    public float playtime;
    
    public PlayerData playerData;
    public WorldData worldData;
    public ProgressData progressData;
    
    public SaveData()
    {
        playerData = new PlayerData();
        worldData = new WorldData();
        progressData = new ProgressData();
        saveDate = DateTime.Now;
        playtime = 0f;
    }
}

/// <summary>
/// Player specific data
/// </summary>
[System.Serializable]
public class PlayerData
{
    // Transform
    public Vector3Data position;
    public Vector3Data rotation;
    
    // Stats
    public int health;
    public int maxHealth;
    public int stability;
    public int maxStability;
    
    // Weapon
    public int currentAmmo;
    public bool hasMap;
    public bool hasMap1;
    public bool hasMap2;
    
    // Inventory
    public List<ItemStackData> items;
    public List<ItemStackData> notes;
    
    public PlayerData()
    {
        position = new Vector3Data();
        rotation = new Vector3Data();
        items = new List<ItemStackData>();
        notes = new List<ItemStackData>();
    }
}

/// <summary>
/// World state data - tracks what's been done in the world
/// </summary>
[System.Serializable]
public class WorldData
{
    // Picked up items (by unique ID)
    public List<string> pickedUpItems;
    
    // Opened containers (by unique ID)
    public List<string> openedContainers;
    
    // Dead enemies (by unique ID)
    public List<string> deadEnemies;
    
    // Moved objects (unique ID + transform data)
    public List<MovedObjectData> movedObjects;
    
    public WorldData()
    {
        pickedUpItems = new List<string>();
        openedContainers = new List<string>();
        deadEnemies = new List<string>();
        movedObjects = new List<MovedObjectData>();
    }
}

/// <summary>
/// Progress and story flags
/// </summary>
[System.Serializable]
public class ProgressData
{
    // Cutscenes that have been played
    public List<string> playedCutscenes;
    
    // Triggered danger zones
    public List<string> triggeredDangerZones;
    
    // Stopped spawners
    public List<string> stoppedSpawners;
    
    // Activated switches/buttons/levers
    public List<string> activatedSwitches;
    
    // Used interactables (DigSpot, puzzles, etc.)
    public List<string> usedInteractables;
    
    // Generic story flags
    public List<string> storyFlags;
    
    public ProgressData()
    {
        playedCutscenes = new List<string>();
        triggeredDangerZones = new List<string>();
        stoppedSpawners = new List<string>();
        activatedSwitches = new List<string>();
        usedInteractables = new List<string>();
        storyFlags = new List<string>();
    }
}

/// <summary>
/// Serializable inventory item data
/// </summary>
[System.Serializable]
public class ItemStackData
{
    public string itemName; // Item ScriptableObject name
    public int quantity;
    
    public ItemStackData(string itemName, int quantity)
    {
        this.itemName = itemName;
        this.quantity = quantity;
    }
}

/// <summary>
/// Serializable Vector3 (Unity's Vector3 isn't serializable by default in JSON)
/// </summary>
[System.Serializable]
public class Vector3Data
{
    public float x;
    public float y;
    public float z;
    
    public Vector3Data()
    {
        x = 0f;
        y = 0f;
        z = 0f;
    }
    
    public Vector3Data(Vector3 vector)
    {
        x = vector.x;
        y = vector.y;
        z = vector.z;
    }
    
    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

/// <summary>
/// Data for moved/repositioned objects
/// </summary>
[System.Serializable]
public class MovedObjectData
{
    public string uniqueID;
    public Vector3Data position;
    public Vector3Data rotation;
    
    public MovedObjectData(string id, Vector3 pos, Vector3 rot)
    {
        uniqueID = id;
        position = new Vector3Data(pos);
        rotation = new Vector3Data(rot);
    }
}
