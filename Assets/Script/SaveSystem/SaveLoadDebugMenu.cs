using UnityEngine;

/// <summary>
/// Simple debug menu for testing save/load
/// Press F5 to save, F9 to load
/// Shows save slots info
/// </summary>
public class SaveLoadDebugMenu : MonoBehaviour
{
    [Header("Keybinds")]
    [SerializeField] private KeyCode saveKey = KeyCode.F5;
    [SerializeField] private KeyCode loadKey = KeyCode.F9;
    [SerializeField] private KeyCode toggleMenuKey = KeyCode.F1;
    
    [Header("Settings")]
    [SerializeField] private int quickSaveSlot = 0;
    
    private bool showMenu = false;
    private Rect menuRect = new Rect(20, 20, 400, 300);
    
    private void Start()
    {
        Debug.Log("SaveLoadDebugMenu initialized! Press F1 to toggle menu, F5 to save, F9 to load");
    }
    
    private void Update()
    {
        // Check if SaveManager exists
        if (SaveManager.instance == null)
        {
            Debug.LogError("SaveManager.instance is null! Make sure SaveManager exists in scene.");
            return;
        }
        
        // Toggle menu
        if (Input.GetKeyDown(toggleMenuKey))
        {
            showMenu = !showMenu;
            Debug.Log($"Debug menu toggled: {(showMenu ? "OPEN" : "CLOSED")}");
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
    
    private void OnGUI()
    {
        if (!showMenu) return;
        
        menuRect = GUI.Window(0, menuRect, DrawMenu, "Save/Load Debug Menu");
    }
    
    private void DrawMenu(int windowID)
    {
        GUILayout.BeginVertical();
        
        GUILayout.Label($"<b>Quick Save/Load</b>", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Label($"F5 = Quick Save | F9 = Quick Load | F1 = Toggle Menu");
        GUILayout.Space(10);
        
        GUILayout.Label($"<b>Save Slots:</b>", new GUIStyle(GUI.skin.label) { richText = true });
        
        // Display all save slots
        for (int i = 0; i < SaveManager.instance.GetTotalSaveSlots(); i++)
        {
            GUILayout.BeginHorizontal();
            
            bool saveExists = SaveManager.instance.DoesSaveExist(i);
            
            if (saveExists)
            {
                SaveData saveData = SaveManager.instance.GetSaveInfo(i);
                if (saveData != null)
                {
                    GUILayout.Label($"Slot {i}: {saveData.sceneName} | {saveData.saveDate:g}");
                    
                    if (GUILayout.Button("Load", GUILayout.Width(60)))
                    {
                        SaveManager.instance.LoadGame(i);
                    }
                    
                    if (GUILayout.Button("Delete", GUILayout.Width(60)))
                    {
                        SaveManager.instance.DeleteSave(i);
                    }
                }
            }
            else
            {
                GUILayout.Label($"Slot {i}: <Empty>");
                
                if (GUILayout.Button("Save", GUILayout.Width(60)))
                {
                    SaveManager.instance.SaveGame(i);
                }
            }
            
            GUILayout.EndHorizontal();
        }
        
        GUILayout.Space(10);
        
        // Save directory info
        if (GUILayout.Button("Open Save Folder"))
        {
            Application.OpenURL(SaveManager.instance.GetSaveDirectory());
        }
        
        GUILayout.EndVertical();
        
        GUI.DragWindow();
    }
}
