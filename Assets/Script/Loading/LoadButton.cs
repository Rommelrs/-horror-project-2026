using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadButton : MonoBehaviour
{
    [SerializeField] string gameSceneName = "Game";
    [SerializeField] string spawnPointID;

    bool isLoading = false;

    //Start a NEW GAME (resets inventory and player state)
    public void NewGame()
    {
        if (isLoading)
            return;

        isLoading = true;
        
        // Clear all save manager runtime tracking for fresh start
        if (SaveManager.instance != null)
            SaveManager.instance.ClearAllRuntimeTracking();

        // Clear checkpoint so Continue button disappears
        if (CheckpointManager.instance != null)
            CheckpointManager.instance.ClearCheckpoint();
        
        // Clear all map markers and map unlock state
        ClearAllMapMarkers();

        // Reset map handler runtime state
        if (MapHandler.instance != null)
        {
            MapHandler.instance.hasMap1 = false;
            MapHandler.instance.hasMap2 = false;
        }
        
        // Mark that we're starting a new game (will be used after scene loads)
        PlayerPrefs.SetInt("IsNewGame", 1);
        PlayerPrefs.Save();

        // Set spawn point before loading
        if (!string.IsNullOrEmpty(spawnPointID))
            PlayerPrefs.SetString("TargetSpawnPoint", spawnPointID);

        // Load the game scene
        if (LoadingHandler.instance)
            LoadingHandler.instance.LoadScene(gameSceneName);
    }
    
    //Load Game scene (does NOT reset player - use for scene transitions)
    public void LoadGame()
    {
        if (isLoading)
            return;

        isLoading = true;

        // Set spawn point before loading
        if (!string.IsNullOrEmpty(spawnPointID))
            PlayerPrefs.SetString("TargetSpawnPoint", spawnPointID);

        // Load the game scene (player state is preserved)
        if (LoadingHandler.instance)
            LoadingHandler.instance.LoadScene(gameSceneName);
    }

    public void LoadGameAfterDelay(float delay)
    {
        StartCoroutine(Co_LoadGameAfterDelay(delay));
    }

    IEnumerator Co_LoadGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        LoadGame();
    }

    //Load Menu Scene
    public void LoadMainMenu()
    {
        if (isLoading)
            return;

        isLoading = true;

        // Load the main menu scene
        if (LoadingHandler.instance)
            LoadingHandler.instance.LoadScene("Menu");
    }

    //Restart Game
    public void RestartGame()
    {
        if (isLoading)
            return;

        isLoading = true;

        // Restart the current scene
        string currentSceneName = SceneManager.GetActiveScene().name;

        if (LoadingHandler.instance)
            LoadingHandler.instance.LoadScene(currentSceneName);
    }

    //Quit Game
    public void QuitGame()
    {
        Application.Quit();
    }
    
    /// <summary>
    /// Clear all map markers from PlayerPrefs
    /// </summary>
    private void ClearAllMapMarkers()
    {
        // Get the registry of all marker IDs
        string markerRegistry = PlayerPrefs.GetString("MarkerIDRegistry", "");
        
        if (!string.IsNullOrEmpty(markerRegistry))
        {
            // Split and delete each registered marker
            string[] markerIDs = markerRegistry.Split(',');
            foreach (string markerID in markerIDs)
            {
                if (!string.IsNullOrEmpty(markerID))
                {
                    PlayerPrefs.DeleteKey($"UIImage_{markerID}");
                }
            }
        }
        
        // Clear all map flags for new game
        PlayerPrefs.SetInt("HasMap", 0);
        PlayerPrefs.DeleteKey("HasMap1");
        PlayerPrefs.DeleteKey("HasMap2");
        
        PlayerPrefs.Save();
    }
}
