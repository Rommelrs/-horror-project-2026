using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager instance;

    [Header("Settings")]
    [SerializeField] string checkpointFileName = "checkpoint.json";
    [SerializeField] float notificationDuration = 2f;

    [Header("Optional UI")]
    [Tooltip("Optional text/panel to briefly show when checkpoint saves (e.g. 'Checkpoint Saved')")]
    [SerializeField] GameObject checkpointNotification;

    string CheckpointFilePath => Path.Combine(Application.persistentDataPath, "Saves", checkpointFileName);

    CheckpointData pendingLoadData; // Held across scene load
    bool pendingRestore = false;

    // Static reference read by CheckpointRestorer in the game scene
    public static CheckpointData pendingRestoreData;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        // Ensure saves directory exists
        string dir = Path.Combine(Application.persistentDataPath, "Saves");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    // ─────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────

    /// <summary>
    /// Call this to save a checkpoint at the current moment.
    /// </summary>
    public void TriggerCheckpoint(string checkpointName = "")
    {
        if (Player.instance == null) return;

        CheckpointData data = CollectData(checkpointName);
        WriteToFile(data);

        if (checkpointNotification != null)
            StartCoroutine(Co_ShowNotification());

    }

    /// <summary>Returns true if a checkpoint file exists.</summary>
    public bool HasCheckpoint()
    {
        return File.Exists(CheckpointFilePath);
    }

    /// <summary>Load the checkpoint - loads the scene then restores state.</summary>
    public void ContinueFromCheckpoint()
    {
        if (!HasCheckpoint()) return;

        string json = File.ReadAllText(CheckpointFilePath);
        pendingLoadData = JsonUtility.FromJson<CheckpointData>(json);

        if (pendingLoadData == null) return;

        SceneManager.sceneLoaded += OnSceneLoadedForCheckpoint;

        if (LoadingHandler.instance != null)
            LoadingHandler.instance.LoadScene(pendingLoadData.sceneName);
        else
            SceneManager.LoadScene(pendingLoadData.sceneName);
    }

    /// <summary>Deletes the checkpoint file (call on New Game).</summary>
    public void ClearCheckpoint()
    {
        if (File.Exists(CheckpointFilePath))
            File.Delete(CheckpointFilePath);
    }

    /// <summary>Returns the checkpoint scene name for display in UI.</summary>
    public string GetCheckpointSceneName()
    {
        if (!HasCheckpoint()) return "";
        try
        {
            string json = File.ReadAllText(CheckpointFilePath);
            CheckpointData data = JsonUtility.FromJson<CheckpointData>(json);
            return data?.sceneName ?? "";
        }
        catch { return ""; }
    }

    // ─────────────────────────────────────────────
    // DATA COLLECTION
    // ─────────────────────────────────────────────

    CheckpointData CollectData(string checkpointName)
    {
        CheckpointData data = new CheckpointData();
        data.checkpointName = checkpointName;
        data.sceneName = SceneManager.GetActiveScene().name;
        data.saveDate = DateTime.Now.ToString();

        Player p = Player.instance;

        // Position & Rotation
        data.posX = p.transform.position.x;
        data.posY = p.transform.position.y;
        data.posZ = p.transform.position.z;
        data.rotY = p.transform.eulerAngles.y;

        // Health
        if (p.health != null)
        {
            data.health = p.health.GetHealthValue();
            data.maxHealth = p.health.GetMaxHealthValue();
        }

        // Stability
        if (p.playerStability != null)
        {
            data.stability = p.playerStability.stability;
            data.maxStability = p.playerStability.maxStability;
        }

        // Weapon
        if (p.playerWeaponSystem != null)
        {
            data.currentAmmo = p.playerWeaponSystem.currentAmmo;
            data.weaponEnabled = p.playerWeaponSystem.weaponIsEnabled;
        }

        // Maps
        if (MapHandler.instance != null)
        {
            data.hasMap1 = MapHandler.instance.hasMap1;
            data.hasMap2 = MapHandler.instance.hasMap2;
        }
        data.hasMap = p.hasMap;

        // Inventory items
        if (p.inventory != null)
        {
            foreach (var stack in p.inventory.GetItems())
                data.items.Add(new ItemSaveEntry(stack.item.itemName, stack.quantity));
            foreach (var note in p.inventory.GetNotes())
                data.notes.Add(new ItemSaveEntry(note.item.itemName, note.quantity));
        }

        // SaveManager runtime tracking
        if (SaveManager.instance != null)
        {
            data.pickedUpItems    = new List<string>(SaveManager.instance.GetPickedUpItems());
            data.deadEnemies      = new List<string>(SaveManager.instance.GetDeadEnemies());
            data.openedContainers = new List<string>(SaveManager.instance.GetOpenedContainers());
            data.activatedSwitches= new List<string>(SaveManager.instance.GetActivatedSwitches());
            data.stoppedSpawners  = new List<string>(SaveManager.instance.GetStoppedSpawners());
            data.triggeredZones   = new List<string>(SaveManager.instance.GetTriggeredZones());
            data.usedInteractables= new List<string>(SaveManager.instance.GetUsedInteractables());
        }

        return data;
    }

    // ─────────────────────────────────────────────
    // RESTORE
    // ─────────────────────────────────────────────

    void Update()
    {
        if (pendingRestore && pendingLoadData != null)
        {
            pendingRestore = false;
            StartCoroutine(Co_RestoreAfterLoad(pendingLoadData));
            pendingLoadData = null;
        }
    }

    void OnSceneLoadedForCheckpoint(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != pendingLoadData.sceneName) return;
        SceneManager.sceneLoaded -= OnSceneLoadedForCheckpoint;

        // Restore runtime tracking IMMEDIATELY before any Start() runs
        if (SaveManager.instance != null)
            SaveManager.instance.RestoreFromCheckpoint(
                pendingLoadData.pickedUpItems, pendingLoadData.deadEnemies, pendingLoadData.openedContainers,
                pendingLoadData.activatedSwitches, pendingLoadData.stoppedSpawners,
                pendingLoadData.triggeredZones, pendingLoadData.usedInteractables);

        // Store data for CheckpointRestorer in the game scene to pick up
        pendingRestoreData = pendingLoadData;
        pendingRestore = true;
    }

    IEnumerator Co_RestoreAfterLoad(CheckpointData data)
    {
        // Wait one frame for scene to initialize
        yield return new WaitForEndOfFrame();

        // Stop ALL playing Timeline directors immediately so cutscenes don't auto-play
        foreach (PlayableDirector director in FindObjectsOfType<PlayableDirector>())
        {
            if (director.state == PlayState.Playing)
                director.Stop();
        }

        yield return new WaitForEndOfFrame();

        Player p = Player.instance;
        if (p == null) yield break;

        // ─ Apply ISaveable.Load() on all objects (removes dead enemies, picked items, etc.)
        yield return new WaitForEndOfFrame();

        // ─ Position
        p.controller.enabled = false;
        yield return new WaitForEndOfFrame();
        p.transform.position = new Vector3(data.posX, data.posY, data.posZ);
        p.transform.rotation = Quaternion.Euler(0, data.rotY, 0);
        yield return new WaitForEndOfFrame();
        p.controller.enabled = true;

        // ─ Health
        if (p.health != null)
        {
            p.health.ResetHealth();
            int dmg = p.health.GetMaxHealthValue() - data.health;
            if (dmg > 0) p.health.Damage(dmg);
        }

        // ─ Stability
        if (p.playerStability != null)
            p.playerStability.stability = data.stability;

        // ─ Weapon
        if (p.playerWeaponSystem != null)
        {
            p.playerWeaponSystem.weaponIsEnabled = data.weaponEnabled;
            p.playerWeaponSystem.currentAmmo = data.currentAmmo;
        }

        // ─ Maps
        p.hasMap = data.hasMap;
        if (MapHandler.instance != null)
        {
            if (data.hasMap1) MapHandler.instance.UnlockMap1();
            if (data.hasMap2) MapHandler.instance.UnlockMap2();
        }

        // ─ Inventory
        if (p.inventory != null)
        {
            p.inventory.ClearInventory();
            yield return new WaitForEndOfFrame();

            foreach (var entry in data.items)
            {
                Item item = Resources.Load<Item>("Items/" + entry.itemName);
                if (item == null) // try subfolders
                    item = FindItemInResources(entry.itemName);
                if (item != null)
                    p.inventory.AddItem(item, entry.quantity);
            }
            foreach (var entry in data.notes)
            {
                Item note = Resources.Load<Item>("Items/Notes/" + entry.itemName);
                if (note == null)
                    note = FindItemInResources(entry.itemName);
                if (note != null)
                    p.inventory.AddItem(note, entry.quantity);
            }
        }

    }

    Item FindItemInResources(string itemName)
    {
        Item[] all = Resources.LoadAll<Item>("Items");
        foreach (var item in all)
            if (item.itemName == itemName || item.name == itemName)
                return item;
        return null;
    }

    // ─────────────────────────────────────────────
    // FILE IO
    // ─────────────────────────────────────────────

    void WriteToFile(CheckpointData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(CheckpointFilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError("[Checkpoint] Failed to save: " + e.Message);
        }
    }

    // ─────────────────────────────────────────────
    // UI
    // ─────────────────────────────────────────────

    IEnumerator Co_ShowNotification()
    {
        if (checkpointNotification == null) yield break;
        checkpointNotification.SetActive(true);
        yield return new WaitForSecondsRealtime(notificationDuration);
        checkpointNotification.SetActive(false);
    }
}

// ─────────────────────────────────────────────
// DATA STRUCTURES
// ─────────────────────────────────────────────

[System.Serializable]
public class CheckpointData
{
    public string checkpointName;
    public string sceneName;
    public string saveDate;

    // Player transform
    public float posX, posY, posZ, rotY;

    // Stats
    public int health, maxHealth;
    public int stability, maxStability;

    // Weapon
    public int currentAmmo;
    public bool weaponEnabled;

    // Maps
    public bool hasMap, hasMap1, hasMap2;

    // Inventory
    public List<ItemSaveEntry> items = new List<ItemSaveEntry>();
    public List<ItemSaveEntry> notes = new List<ItemSaveEntry>();

    // World state
    public List<string> pickedUpItems    = new List<string>();
    public List<string> deadEnemies      = new List<string>();
    public List<string> openedContainers = new List<string>();
    public List<string> activatedSwitches= new List<string>();
    public List<string> stoppedSpawners  = new List<string>();
    public List<string> triggeredZones   = new List<string>();
    public List<string> usedInteractables= new List<string>();
}

[System.Serializable]
public class ItemSaveEntry
{
    public string itemName;
    public int quantity;
    public ItemSaveEntry(string name, int qty) { itemName = name; quantity = qty; }
}
