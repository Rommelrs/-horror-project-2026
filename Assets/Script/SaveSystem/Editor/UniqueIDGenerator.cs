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
        
        // Mark the scene as dirty so Unity prompts to save
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        
        Debug.Log($"<color=green>UniqueID Generation Complete!</color> Generated: {generatedCount}, Skipped: {skippedCount}");
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
}
