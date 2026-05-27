using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor utility to generate UniqueIDs for all objects in the current scene
/// </summary>
public class UniqueIDGenerator : Editor
{
    [MenuItem("Tools/Save System/Generate All UniqueIDs in Scene")]
    public static void GenerateAllUniqueIDs()
    {
        Debug.Log("[SaveSystem] Starting UniqueID generation...");

        // Find all UniqueID components in the scene (including inactive objects)
        UniqueID[] allIDs = FindObjectsByType<UniqueID>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        int generatedCount = 0;
        int skippedCount = 0;
        
        foreach (UniqueID uniqueID in allIDs)
        {
            // Access the ID property to trigger generation if needed
            string id = uniqueID.ID;
            
            // Force the object to be marked as dirty so Unity saves the change
            EditorUtility.SetDirty(uniqueID);
            
            if (!string.IsNullOrEmpty(id))
            {
                generatedCount++;
                Debug.Log($"Generated/Verified ID for: {uniqueID.gameObject.name} ({id})");
            }
            else
            {
                skippedCount++;
            }
        }
        
        // Generate IDs for SaveableUIImage markers
        SaveableUIImage[] allUIImages = FindObjectsByType<SaveableUIImage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int markerIDCount = 0;
        
        foreach (SaveableUIImage uiImage in allUIImages)
        {
            if (uiImage.markers == null || uiImage.markers.Length == 0)
                continue;
            
            for (int i = 0; i < uiImage.markers.Length; i++)
            {
                if (string.IsNullOrEmpty(uiImage.markers[i].uniqueID))
                {
                    // Auto-generate ID based on marker GameObject name
                    if (uiImage.markers[i].marker != null)
                    {
                        uiImage.markers[i].uniqueID = $"{uiImage.markers[i].marker.name}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
                        markerIDCount++;
                        Debug.Log($"Generated marker ID: {uiImage.markers[i].uniqueID} for {uiImage.markers[i].marker.name}");
                    }
                }
            }
            
            EditorUtility.SetDirty(uiImage);
        }
        
        // Mark the scene as dirty so Unity prompts to save
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        
        Debug.Log($"<color=green>UniqueID Generation Complete!</color> Generated: {generatedCount}, Skipped: {skippedCount}, Marker IDs: {markerIDCount}");
        Debug.Log("<color=yellow>Remember to save your scene (Ctrl+S) to persist the IDs!</color>");
    }
    
    [MenuItem("Tools/Save System/Regenerate All UniqueIDs in Scene (CAREFUL!)")]
    public static void RegenerateAllUniqueIDs()
    {
        if (!EditorUtility.DisplayDialog("Regenerate All UniqueIDs", 
            "This will generate NEW IDs for ALL objects with UniqueID components in the scene. " +
            "This will break existing save files! Are you sure?", 
            "Yes, Regenerate", "Cancel"))
        {
            return;
        }

        Debug.Log("[SaveSystem] Starting UniqueID regeneration...");
        
        // Find all UniqueID components in the scene (including inactive objects)
        UniqueID[] allIDs = FindObjectsByType<UniqueID>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        int regeneratedCount = 0;
        
        foreach (UniqueID uniqueID in allIDs)
        {
            uniqueID.GenerateNewID();
            EditorUtility.SetDirty(uniqueID);
            regeneratedCount++;
        }
        
        // Mark the scene as dirty
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        
        Debug.Log($"<color=green>Regenerated {regeneratedCount} UniqueIDs!</color>");
        Debug.Log("<color=yellow>Remember to save your scene (Ctrl+S)!</color>");
    }
    
    [MenuItem("Tools/Save System/Clear All Map Marker PlayerPrefs")]
    public static void ClearMapMarkerPrefs()
    {
        if (!EditorUtility.DisplayDialog("Clear Map Markers", 
            "This will clear all saved map marker states from PlayerPrefs. Continue?", 
            "Yes, Clear", "Cancel"))
        {
            return;
        }
        
        // Find all SaveableUIImage components
        SaveableUIImage[] allUIImages = FindObjectsByType<SaveableUIImage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int clearedCount = 0;
        
        foreach (SaveableUIImage uiImage in allUIImages)
        {
            if (uiImage.markers == null)
                continue;
            
            foreach (var marker in uiImage.markers)
            {
                if (!string.IsNullOrEmpty(marker.uniqueID))
                {
                    UnityEngine.PlayerPrefs.DeleteKey($"UIImage_{marker.uniqueID}");
                    clearedCount++;
                }
            }
        }
        
        // Also clear HasMap flag
        UnityEngine.PlayerPrefs.DeleteKey("HasMap");
        
        UnityEngine.PlayerPrefs.Save();
        Debug.Log($"<color=green>Cleared {clearedCount} map marker PlayerPrefs and HasMap flag!</color>");
    }
}
