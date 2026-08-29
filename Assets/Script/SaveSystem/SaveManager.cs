using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Core save system manager - handles all save/load operations
/// Singleton pattern for global access
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;
    
    [Header("Settings")]
    [SerializeField] private int totalSaveSlots = 5;
    [SerializeField] private bool useEncryption = false; // For future if you want to encrypt saves
    
    private string saveDirectory;
    private SaveData currentSaveData;
    private float sessionStartTime;
    
    // Runtime tracking - items picked up this session (persists until save)
    // Made static so they persist even if instance is temporarily replaced
    private static HashSet<string> runtimePickedUpItems = new HashSet<string>();
    private static HashSet<string> runtimeDeadEnemies = new HashSet<string>();
    private static HashSet<string> runtimeOpenedContainers = new HashSet<string>();
    private static HashSet<string> runtimeActivatedSwitches = new HashSet<string>();
    private static HashSet<string> runtimeStoppedSpawners = new HashSet<string>();
    private static HashSet<string> runtimeTriggeredZones = new HashSet<string>();
    private static HashSet<string> runtimeUsedInteractables = new HashSet<string>();
    
    // Events
    public delegate void SaveLoadEvent();
    public event SaveLoadEvent OnGameSaved;
    public event SaveLoadEvent OnGameLoaded;
    
    private void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
            
            // Create saves directory if it doesn't exist
            if (!Directory.Exists(saveDirectory))
            {
                Directory.CreateDirectory(saveDirectory);
            }
            
            sessionStartTime = Time.time;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    #region Save Operations
    
    /// <summary>
    /// Save the game to a specific slot
    /// </summary>
    public void SaveGame(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= totalSaveSlots)
        {
            Debug.LogError($"Invalid save slot: {slotIndex}");
            return;
        }
        
        // Create new save data
        SaveData saveData = new SaveData();
        saveData.saveFileName = GetSaveFileName(slotIndex);
        saveData.sceneName = SceneManager.GetActiveScene().name;
        saveData.saveDate = DateTime.Now;
        saveData.playtime = Time.time - sessionStartTime;
        
        // Collect data from all saveable objects
        CollectSaveData(saveData);
        
        // Serialize to JSON
        string json = JsonUtility.ToJson(saveData, true);
        
        // Write to file
        string filePath = GetSaveFilePath(slotIndex);
        try
        {
            File.WriteAllText(filePath, json);
            currentSaveData = saveData;
            OnGameSaved?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save game: {e.Message}");
        }
    }
    
    /// <summary>
    /// Collect save data from all saveable objects in the scene
    /// </summary>
    private void CollectSaveData(SaveData saveData)
    {
        // Add runtime tracked items to save data
        foreach (string itemID in runtimePickedUpItems)
        {
            if (!saveData.worldData.pickedUpItems.Contains(itemID))
            {
                saveData.worldData.pickedUpItems.Add(itemID);
            }
        }
        
        foreach (string enemyID in runtimeDeadEnemies)
        {
            if (!saveData.worldData.deadEnemies.Contains(enemyID))
            {
                saveData.worldData.deadEnemies.Add(enemyID);
            }
        }
        
        foreach (string containerID in runtimeOpenedContainers)
        {
            if (!saveData.worldData.openedContainers.Contains(containerID))
            {
                saveData.worldData.openedContainers.Add(containerID);
            }
        }
        
        foreach (string switchID in runtimeActivatedSwitches)
        {
            if (!saveData.progressData.activatedSwitches.Contains(switchID))
            {
                saveData.progressData.activatedSwitches.Add(switchID);
            }
        }
        
        foreach (string spawnerID in runtimeStoppedSpawners)
        {
            if (!saveData.progressData.stoppedSpawners.Contains(spawnerID))
            {
                saveData.progressData.stoppedSpawners.Add(spawnerID);
            }
        }
        
        foreach (string zoneID in runtimeTriggeredZones)
        {
            if (!saveData.progressData.triggeredDangerZones.Contains(zoneID))
            {
                saveData.progressData.triggeredDangerZones.Add(zoneID);
            }
        }
        
        foreach (string interactableID in runtimeUsedInteractables)
        {
            if (!saveData.progressData.usedInteractables.Contains(interactableID))
            {
                saveData.progressData.usedInteractables.Add(interactableID);
            }
        }
        
        // Find all objects with ISaveable interface
        ISaveable[] saveables = FindObjectsOfType<MonoBehaviour>() as ISaveable[];
        
        // This won't work directly, need to check each MonoBehaviour
        MonoBehaviour[] allObjects = FindObjectsOfType<MonoBehaviour>();
        
        int saveableCount = 0;
        foreach (MonoBehaviour obj in allObjects)
        {
            if (obj is ISaveable saveable)
            {
                saveableCount++;
                saveable.Save(saveData);
            }
        }
        
    }
    
    #endregion
    
    #region Load Operations
    
    /// <summary>
    /// Load the game from a specific slot
    /// </summary>
    public void LoadGame(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= totalSaveSlots)
        {
            Debug.LogError($"Invalid save slot: {slotIndex}");
            return;
        }
        
        string filePath = GetSaveFilePath(slotIndex);
        
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"No save file found at slot {slotIndex}");
            return;
        }
        
        try
        {
            // Read JSON from file
            string json = File.ReadAllText(filePath);
            
            // Deserialize
            SaveData saveData = JsonUtility.FromJson<SaveData>(json);
            
            if (saveData == null)
            {
                Debug.LogError("Failed to deserialize save data");
                return;
            }
            
            currentSaveData = saveData;
            
            // Load the saved scene
            SceneManager.sceneLoaded += OnSceneLoadedForLoad;
            SceneManager.LoadScene(saveData.sceneName);
            
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load game: {e.Message}");
        }
    }
    
    /// <summary>
    /// Apply loaded data after scene has loaded
    /// </summary>
    private void OnSceneLoadedForLoad(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoadedForLoad;
        
        if (currentSaveData != null)
        {
            ApplySaveData(currentSaveData);
            OnGameLoaded?.Invoke();
        }
    }
    
    /// <summary>
    /// Apply save data to all saveable objects in the scene
    /// </summary>
    private void ApplySaveData(SaveData saveData)
    {
        // Clear runtime tracking and reload from save
        runtimePickedUpItems.Clear();
        runtimeDeadEnemies.Clear();
        runtimeOpenedContainers.Clear();
        runtimeActivatedSwitches.Clear();
        runtimeStoppedSpawners.Clear();
        runtimeTriggeredZones.Clear();
        runtimeUsedInteractables.Clear();
        
        // Populate runtime tracking from loaded save
        if (saveData.worldData != null)
        {
            
            foreach (string id in saveData.worldData.pickedUpItems)
            {
                runtimePickedUpItems.Add(id);
            }
            foreach (string id in saveData.worldData.deadEnemies)
                runtimeDeadEnemies.Add(id);
            foreach (string id in saveData.worldData.openedContainers)
                runtimeOpenedContainers.Add(id);
        }
        else
        {
            Debug.LogWarning("saveData.worldData is null!");
        }
        
        if (saveData.progressData != null)
        {
            foreach (string id in saveData.progressData.activatedSwitches)
                runtimeActivatedSwitches.Add(id);
            foreach (string id in saveData.progressData.stoppedSpawners)
                runtimeStoppedSpawners.Add(id);
            foreach (string id in saveData.progressData.triggeredDangerZones)
                runtimeTriggeredZones.Add(id);
            foreach (string id in saveData.progressData.usedInteractables)
                runtimeUsedInteractables.Add(id);
        }
        
        MonoBehaviour[] allObjects = FindObjectsOfType<MonoBehaviour>();
        
        foreach (MonoBehaviour obj in allObjects)
        {
            if (obj is ISaveable saveable)
            {
                saveable.Load(saveData);
            }
        }
    }
    
    #endregion
    
    #region Runtime World State Tracking
    
    /// <summary>
    /// Register an item as picked up (call this immediately when item is picked up)
    /// </summary>
    public void RegisterPickedUpItem(string itemID)
    {
        if (!runtimePickedUpItems.Contains(itemID))
        {
            runtimePickedUpItems.Add(itemID);
        }
    }
    
    /// <summary>
    /// Register an enemy as dead (call this immediately when enemy dies)
    /// </summary>
    public void RegisterDeadEnemy(string enemyID)
    {
        if (!runtimeDeadEnemies.Contains(enemyID))
        {
            runtimeDeadEnemies.Add(enemyID);
        }
    }
    
    /// <summary>
    /// Register a container as opened (call this immediately when container opens)
    /// </summary>
    public void RegisterOpenedContainer(string containerID)
    {
        if (!runtimeOpenedContainers.Contains(containerID))
        {
            runtimeOpenedContainers.Add(containerID);
        }
    }
    
    /// <summary>
    /// Check if an item was picked up this session
    /// </summary>
    public bool IsItemPickedUp(string itemID)
    {
        bool result = runtimePickedUpItems.Contains(itemID);
        return result;
    }
    
    /// <summary>
    /// Check if an enemy is dead this session
    /// </summary>
    public bool IsEnemyDead(string enemyID)
    {
        return runtimeDeadEnemies.Contains(enemyID);
    }
    
    /// <summary>
    /// Check if a container was opened this session
    /// </summary>
    public bool IsContainerOpened(string containerID)
    {
        return runtimeOpenedContainers.Contains(containerID);
    }
    
    /// <summary>
    /// Register a switch/button as activated
    /// </summary>
    public void RegisterActivatedSwitch(string switchID)
    {
        if (!runtimeActivatedSwitches.Contains(switchID))
        {
            runtimeActivatedSwitches.Add(switchID);
        }
    }
    
    /// <summary>
    /// Check if a switch was activated this session
    /// </summary>
    public bool IsSwitchActivated(string switchID)
    {
        return runtimeActivatedSwitches.Contains(switchID);
    }
    
    /// <summary>
    /// Register a spawner as stopped
    /// </summary>
    public void RegisterStoppedSpawner(string spawnerID)
    {
        if (!runtimeStoppedSpawners.Contains(spawnerID))
        {
            runtimeStoppedSpawners.Add(spawnerID);
        }
    }
    
    /// <summary>
    /// Check if a spawner was stopped this session
    /// </summary>
    public bool IsSpawnerStopped(string spawnerID)
    {
        return runtimeStoppedSpawners.Contains(spawnerID);
    }
    
    /// <summary>
    /// Register a trigger zone as activated
    /// </summary>
    public void RegisterTriggeredZone(string zoneID)
    {
        if (!runtimeTriggeredZones.Contains(zoneID))
        {
            runtimeTriggeredZones.Add(zoneID);
        }
    }
    
    /// <summary>
    /// Check if a trigger zone was already activated
    /// </summary>
    public bool IsZoneTriggered(string zoneID)
    {
        return runtimeTriggeredZones.Contains(zoneID);
    }
    
    /// <summary>
    /// Register an interactable as used
    /// </summary>
    public void RegisterUsedInteractable(string interactableID)
    {
        if (!runtimeUsedInteractables.Contains(interactableID))
        {
            runtimeUsedInteractables.Add(interactableID);
        }
    }
    
    /// <summary>
    /// Check if an interactable was already used
    /// </summary>
    public bool IsInteractableUsed(string interactableID)
    {
        return runtimeUsedInteractables.Contains(interactableID);
    }
    
    // ─── Getters for CheckpointManager ───
    public HashSet<string> GetPickedUpItems()     => new HashSet<string>(runtimePickedUpItems);
    public HashSet<string> GetDeadEnemies()       => new HashSet<string>(runtimeDeadEnemies);
    public HashSet<string> GetOpenedContainers()  => new HashSet<string>(runtimeOpenedContainers);
    public HashSet<string> GetActivatedSwitches() => new HashSet<string>(runtimeActivatedSwitches);
    public HashSet<string> GetStoppedSpawners()   => new HashSet<string>(runtimeStoppedSpawners);
    public HashSet<string> GetTriggeredZones()    => new HashSet<string>(runtimeTriggeredZones);
    public HashSet<string> GetUsedInteractables() => new HashSet<string>(runtimeUsedInteractables);

    /// <summary>Restore runtime tracking from checkpoint data.</summary>
    public void RestoreFromCheckpoint(
        List<string> pickedUp, List<string> dead, List<string> containers,
        List<string> switches, List<string> spawners,
        List<string> zones, List<string> interactables)
    {
        runtimePickedUpItems.Clear();     foreach (var id in pickedUp)      runtimePickedUpItems.Add(id);
        runtimeDeadEnemies.Clear();       foreach (var id in dead)          runtimeDeadEnemies.Add(id);
        runtimeOpenedContainers.Clear();  foreach (var id in containers)    runtimeOpenedContainers.Add(id);
        runtimeActivatedSwitches.Clear(); foreach (var id in switches)      runtimeActivatedSwitches.Add(id);
        runtimeStoppedSpawners.Clear();   foreach (var id in spawners)      runtimeStoppedSpawners.Add(id);
        runtimeTriggeredZones.Clear();    foreach (var id in zones)         runtimeTriggeredZones.Add(id);
        runtimeUsedInteractables.Clear(); foreach (var id in interactables) runtimeUsedInteractables.Add(id);
    }

    /// <summary>
    /// Clear all runtime tracking - use when starting a completely new game
    /// </summary>
    public void ClearAllRuntimeTracking()
    {
        runtimePickedUpItems.Clear();
        runtimeDeadEnemies.Clear();
        runtimeOpenedContainers.Clear();
        runtimeActivatedSwitches.Clear();
        runtimeStoppedSpawners.Clear();
        runtimeTriggeredZones.Clear();
        runtimeUsedInteractables.Clear();
        Debug.Log("[SaveManager] All runtime tracking cleared for new game");
    }
    
    #endregion
    
    #region Save Slot Management
    
    /// <summary>
    /// Check if a save slot has data
    /// </summary>
    public bool DoesSaveExist(int slotIndex)
    {
        string filePath = GetSaveFilePath(slotIndex);
        return File.Exists(filePath);
    }
    
    /// <summary>
    /// Get save info for a slot (for UI display)
    /// </summary>
    public SaveData GetSaveInfo(int slotIndex)
    {
        if (!DoesSaveExist(slotIndex))
            return null;
        
        try
        {
            string filePath = GetSaveFilePath(slotIndex);
            string json = File.ReadAllText(filePath);
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to read save info: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Delete a save slot
    /// </summary>
    public void DeleteSave(int slotIndex)
    {
        string filePath = GetSaveFilePath(slotIndex);
        
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to delete save: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// Get total number of save slots
    /// </summary>
    public int GetTotalSaveSlots()
    {
        return totalSaveSlots;
    }
    
    /// <summary>
    /// Get save slot metadata for UI display
    /// </summary>
    public SaveSlotData GetSaveSlotInfo(int slotIndex)
    {
        SaveSlotData slotData = new SaveSlotData(slotIndex);
        
        if (!DoesSaveExist(slotIndex))
        {
            slotData.isEmpty = true;
            return slotData;
        }
        
        try
        {
            SaveData saveData = GetSaveInfo(slotIndex);
            
            if (saveData != null)
            {
                slotData.isEmpty = false;
                slotData.sceneName = saveData.sceneName;
                slotData.saveDate = saveData.saveDate;
                slotData.playtime = saveData.playtime;
                
                if (saveData.playerData != null)
                {
                    slotData.playerHealth = saveData.playerData.health;
                    slotData.playerMaxHealth = saveData.playerData.maxHealth;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to read save slot info: {e.Message}");
            slotData.isEmpty = true;
        }
        
        return slotData;
    }
    
    #endregion
    
    #region File Path Helpers
    
    private string GetSaveFileName(int slotIndex)
    {
        return $"SaveSlot_{slotIndex}.json";
    }
    
    private string GetSaveFilePath(int slotIndex)
    {
        return Path.Combine(saveDirectory, GetSaveFileName(slotIndex));
    }
    
    /// <summary>
    /// Get the save directory path (for debug/info purposes)
    /// </summary>
    public string GetSaveDirectory()
    {
        return saveDirectory;
    }
    
    #endregion
}
