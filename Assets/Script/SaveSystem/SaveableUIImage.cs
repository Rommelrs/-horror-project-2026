using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Saves the active state of UI Image child GameObjects
/// IMPORTANT: Put this on an ALWAYS-ACTIVE GameObject (e.g. GameManager, EventSystem, or a dedicated MapMarkerManager)
/// NOT on the map UI parent (which may be inactive)
/// Each CHILD marker gets tracked individually with its own save data
/// When a child is enabled via OnPlayerTrigger, it saves to PlayerPrefs
/// On load, it restores each child's state
/// </summary>
public class SaveableUIImage : MonoBehaviour
{
    [System.Serializable]
    public class MarkerData
    {
        public GameObject marker;
        [Tooltip("Unique ID for this marker - MUST be unique and never change!")]
        public string uniqueID;
        [HideInInspector] public bool lastKnownState = false;
    }
    
    [Header("Markers")]
    [Tooltip("Add all child UI Image GameObjects here (each one starts disabled)")]
    public MarkerData[] markers;
    
    private bool hasInitialized = false;
    
    private void Start()
    {
        InitializeMarkers();
    }
    
    private void InitializeMarkers()
    {
        if (hasInitialized)
            return;
        
        if (markers == null || markers.Length == 0)
        {
            Debug.LogError($"[SaveableUIImage] No markers assigned on {gameObject.name}!");
            return;
        }
        
        hasInitialized = true;
        
        // Check if this is a new game (clear markers)
        bool isNewGame = PlayerPrefs.GetInt("IsNewGame", 0) == 1;
        if (isNewGame)
        {
            // Clear all marker data for new game
            foreach (var markerData in markers)
            {
                if (!string.IsNullOrEmpty(markerData.uniqueID))
                {
                    PlayerPrefs.DeleteKey($"UIImage_{markerData.uniqueID}");
                }
            }
            
            // Clear the new game flag (only clear once)
            PlayerPrefs.SetInt("IsNewGame", 0);
            PlayerPrefs.Save();
        }
        
        // Validate that all markers have unique IDs
        for (int i = 0; i < markers.Length; i++)
        {
            if (string.IsNullOrEmpty(markers[i].uniqueID))
            {
                Debug.LogError($"[SaveableUIImage] Marker {i} on {gameObject.name} is missing a uniqueID! Please set it in the inspector.");
            }
        }
        
        // Load saved state for each marker
        foreach (var markerData in markers)
        {
            if (markerData.marker == null)
                continue;
            
            bool wasRevealed = PlayerPrefs.GetInt($"UIImage_{markerData.uniqueID}", 0) == 1;
            markerData.marker.SetActive(wasRevealed);
            markerData.lastKnownState = wasRevealed;
        }
    }
    
    private void Update()
    {
        if (markers == null)
            return;
        
        // Check each marker for state changes
        foreach (var markerData in markers)
        {
            if (markerData.marker == null)
                continue;
            
            // Detect when child state changes (player triggered it)
            if (markerData.marker.activeSelf != markerData.lastKnownState)
            {
                markerData.lastKnownState = markerData.marker.activeSelf;
                
                if (markerData.marker.activeSelf)
                {
                    // Child was just enabled, save it
                    PlayerPrefs.SetInt($"UIImage_{markerData.uniqueID}", 1);
                    PlayerPrefs.Save();
                }
            }
        }
    }
}
