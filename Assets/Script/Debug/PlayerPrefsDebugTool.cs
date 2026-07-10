using UnityEngine;

/// <summary>
/// Debug tool to test New Game / Load Game behavior in Editor
/// Press keys to simulate main menu actions
/// </summary>
public class PlayerPrefsDebugTool : MonoBehaviour
{
    [Header("Debug Keys")]
    [SerializeField] private KeyCode simulateNewGameKey = KeyCode.F2;
    [SerializeField] private KeyCode simulateLoadGameKey = KeyCode.F3;
    [SerializeField] private KeyCode showMarkerStatusKey = KeyCode.F4;
    [SerializeField] private KeyCode clearAllPlayerPrefsKey = KeyCode.F10;
    
    private void Update()
    {
        // Simulate New Game
        if (Input.GetKeyDown(simulateNewGameKey))
        {
            Debug.Log("=== SIMULATING NEW GAME ===");
            SimulateNewGame();
        }
        
        // Simulate Load Game
        if (Input.GetKeyDown(simulateLoadGameKey))
        {
            Debug.Log("=== SIMULATING LOAD GAME ===");
            SimulateLoadGame();
        }
        
        // Show marker status
        if (Input.GetKeyDown(showMarkerStatusKey))
        {
            ShowMarkerStatus();
        }
        
        // Clear ALL PlayerPrefs (nuclear option)
        if (Input.GetKeyDown(clearAllPlayerPrefsKey))
        {
            Debug.LogWarning("=== CLEARING ALL PLAYERPREFS ===");
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("All PlayerPrefs cleared!");
        }
    }
    
    private void SimulateNewGame()
    {
        // Same logic as MainMenuController.ClearAllMapMarkerPlayerPrefs()
        string markerRegistry = PlayerPrefs.GetString("MarkerIDRegistry", "");
        
        if (!string.IsNullOrEmpty(markerRegistry))
        {
            string[] markerIDs = markerRegistry.Split(',');
            foreach (string markerID in markerIDs)
            {
                if (!string.IsNullOrEmpty(markerID))
                {
                    PlayerPrefs.DeleteKey($"UIImage_{markerID}");
                    Debug.Log($"  Cleared marker: UIImage_{markerID}");
                }
            }
            
            Debug.Log($"Cleared {markerIDs.Length} map markers");
        }
        else
        {
            Debug.LogWarning("No marker registry found! Markers may not have registered yet.");
        }
        
        PlayerPrefs.SetInt("IsNewGame", 1);
        PlayerPrefs.SetInt("HasMap", 0);
        PlayerPrefs.Save();
        
        Debug.Log("New Game simulation complete. Restart scene to see effect.");
    }
    
    private void SimulateLoadGame()
    {
        PlayerPrefs.SetInt("IsNewGame", 0);
        PlayerPrefs.Save();
        
        Debug.Log("Load Game simulation complete. Markers should be preserved.");
    }
    
    private void ShowMarkerStatus()
    {
        Debug.Log("=== MARKER STATUS ===");
        
        // Show registry
        string markerRegistry = PlayerPrefs.GetString("MarkerIDRegistry", "");
        if (!string.IsNullOrEmpty(markerRegistry))
        {
            Debug.Log($"Registry: {markerRegistry}");
            
            string[] markerIDs = markerRegistry.Split(',');
            Debug.Log($"Total markers in registry: {markerIDs.Length}");
            
            // Show each marker's state
            foreach (string markerID in markerIDs)
            {
                if (!string.IsNullOrEmpty(markerID))
                {
                    int state = PlayerPrefs.GetInt($"UIImage_{markerID}", 0);
                    Debug.Log($"  {markerID}: {(state == 1 ? "REVEALED" : "Hidden")}");
                }
            }
        }
        else
        {
            Debug.LogWarning("No marker registry found!");
        }
        
        // Show flags
        Debug.Log($"IsNewGame flag: {PlayerPrefs.GetInt("IsNewGame", 0)}");
        Debug.Log($"HasMap flag: {PlayerPrefs.GetInt("HasMap", 0)}");
    }
    
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 400, 200));
        GUILayout.Label("=== PlayerPrefs Debug Tool ===");
        GUILayout.Label($"F2 - Simulate New Game");
        GUILayout.Label($"F3 - Simulate Load Game");
        GUILayout.Label($"F4 - Show Marker Status");
        GUILayout.Label($"F10 - Clear ALL PlayerPrefs");
        GUILayout.EndArea();
    }
}
