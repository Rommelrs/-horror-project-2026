using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main menu controller - handles button clicks for New Game, Load Game, Options, Exit
/// Works with UIButton for sound effects and LoadingHandler for scene loading
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button exitButton;
    
    [Header("Scene Settings")]
    [SerializeField] private string firstGameSceneName = "Game"; // Change this to your actual first game scene name
    
    private void Start()
    {
        // Setup button listeners
        if (newGameButton != null)
            newGameButton.onClick.AddListener(OnNewGame);
        
        if (loadGameButton != null)
        {
            loadGameButton.onClick.AddListener(OnLoadGame);
            UpdateLoadButtonState();
        }
        
        if (optionsButton != null)
            optionsButton.onClick.AddListener(OnOptions);
        
        if (exitButton != null)
            exitButton.onClick.AddListener(OnExit);
    }
    
    /// <summary>
    /// Update load button state based on whether saves exist
    /// </summary>
    private void UpdateLoadButtonState()
    {
        if (loadGameButton == null) return;
        
        // Check if SaveManager and SaveLoadMenuManager exist
        if (SaveManager.instance != null && SaveLoadMenuManager.instance != null)
        {
            bool hasSaves = SaveLoadMenuManager.instance.DoAnySavesExist();
            loadGameButton.interactable = hasSaves;
        }
        else
        {
            // If managers aren't ready yet, try again next frame
            StartCoroutine(UpdateLoadButtonStateNextFrame());
        }
    }
    
    private System.Collections.IEnumerator UpdateLoadButtonStateNextFrame()
    {
        yield return null;
        UpdateLoadButtonState();
    }
    
    /// <summary>
    /// Start a new game
    /// </summary>
    private void OnNewGame()
    {
        // Load the first game scene using LoadingHandler
        if (LoadingHandler.instance != null)
        {
            LoadingHandler.instance.LoadScene(firstGameSceneName);
        }
        else
        {
            Debug.LogWarning("LoadingHandler instance not found! Loading scene directly.");
            UnityEngine.SceneManagement.SceneManager.LoadScene(firstGameSceneName);
        }
    }
    
    /// <summary>
    /// Open load game menu
    /// </summary>
    private void OnLoadGame()
    {
        // Open the load menu
        if (SaveLoadMenuManager.instance != null)
        {
            SaveLoadMenuManager.instance.OpenLoadMenu();
        }
        else
        {
            Debug.LogWarning("SaveLoadMenuManager instance not found!");
        }
    }
    
    /// <summary>
    /// Open options menu
    /// </summary>
    private void OnOptions()
    {
        // TODO: Implement options menu
        Debug.Log("Options menu - not yet implemented");
    }
    
    /// <summary>
    /// Exit the game
    /// </summary>
    private void OnExit()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
