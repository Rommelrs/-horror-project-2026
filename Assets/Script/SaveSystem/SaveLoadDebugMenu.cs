using UnityEngine;

/// <summary>
/// Quick save / load keys for testing: F5 to save, F9 to load
/// </summary>
public class SaveLoadDebugMenu : MonoBehaviour
{
    [Header("Keybinds")]
    [SerializeField] private KeyCode saveKey = KeyCode.F5;
    [SerializeField] private KeyCode loadKey = KeyCode.F9;

    [Header("Settings")]
    [SerializeField] private int quickSaveSlot = 0;

    private void Update()
    {
        // Check if SaveManager exists
        if (SaveManager.instance == null)
        {
            Debug.LogError("SaveManager.instance is null! Make sure SaveManager exists in scene.");
            return;
        }

        // Quick save
        if (Input.GetKeyDown(saveKey))
        {
            SaveManager.instance.SaveGame(quickSaveSlot);
            Debug.Log($"<color=green>QUICK SAVED to slot {quickSaveSlot}</color>");
        }

        // Quick load
        if (Input.GetKeyDown(loadKey))
        {
            SaveManager.instance.LoadGame(quickSaveSlot);
            Debug.Log($"<color=cyan>QUICK LOADED from slot {quickSaveSlot}</color>");
        }
    }
}
