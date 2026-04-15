using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manager for save/load menus - can be accessed from pause menu or main menu
/// Handles input for opening save/load menus
/// </summary>
public class SaveLoadMenuManager : MonoBehaviour
{
    public static SaveLoadMenuManager instance;
    
    [Header("UI References")]
    [SerializeField] private SaveMenuUI saveMenuUI;
    [SerializeField] private LoadMenuUI loadMenuUI;
    
    [Header("Quick Save/Load (Optional)")]
    [SerializeField] private bool enableQuickSaveLoad = true;
    [SerializeField] private int quickSaveSlotIndex = 0; // Slot 0 for quick saves
    [SerializeField] private InputActionReference quickSaveInput; // F5
    [SerializeField] private InputActionReference quickLoadInput; // F9
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip quickSaveSound;
    [SerializeField] private AudioClip quickLoadSound;
    
    private void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }
    
    private void OnEnable()
    {
        if (enableQuickSaveLoad)
        {
            if (quickSaveInput != null)
            {
                quickSaveInput.action.Enable();
                quickSaveInput.action.performed += OnQuickSave;
            }
            
            if (quickLoadInput != null)
            {
                quickLoadInput.action.Enable();
                quickLoadInput.action.performed += OnQuickLoad;
            }
        }
    }
    
    private void OnDisable()
    {
        if (quickSaveInput != null)
        {
            quickSaveInput.action.performed -= OnQuickSave;
            quickSaveInput.action.Disable();
        }
        
        if (quickLoadInput != null)
        {
            quickLoadInput.action.performed -= OnQuickLoad;
            quickLoadInput.action.Disable();
        }
    }
    
    /// <summary>
    /// Open the save menu (called from pause menu button)
    /// </summary>
    public void OpenSaveMenu()
    {
        if (saveMenuUI != null)
        {
            saveMenuUI.OpenSaveMenu();
        }
        else
        {
            Debug.LogWarning("SaveMenuUI is not assigned!");
        }
    }
    
    /// <summary>
    /// Open the load menu (called from pause/main menu button)
    /// </summary>
    public void OpenLoadMenu()
    {
        if (loadMenuUI != null)
        {
            loadMenuUI.OpenLoadMenu();
        }
        else
        {
            Debug.LogWarning("LoadMenuUI is not assigned!");
        }
    }
    
    /// <summary>
    /// Quick save to dedicated slot (F5 by default)
    /// </summary>
    private void OnQuickSave(InputAction.CallbackContext context)
    {
        if (!enableQuickSaveLoad) return;
        
        // Don't quick save during cutscenes or menus
        if (GameManager.IsPaused) return;
        
        // Play sound
        if (audioSource != null && quickSaveSound != null)
            audioSource.PlayOneShot(quickSaveSound);
        
        // Save to quick save slot
        SaveManager.instance.SaveGame(quickSaveSlotIndex);
        
        Debug.Log($"Quick saved to slot {quickSaveSlotIndex}!");
        
        // You can show a quick save notification UI here
        ShowQuickSaveNotification();
    }
    
    /// <summary>
    /// Quick load from dedicated slot (F9 by default)
    /// </summary>
    private void OnQuickLoad(InputAction.CallbackContext context)
    {
        if (!enableQuickSaveLoad) return;
        
        // Don't quick load during cutscenes
        if (GameManager.IsPaused) return;
        
        // Check if quick save exists
        if (!SaveManager.instance.DoesSaveExist(quickSaveSlotIndex))
        {
            Debug.LogWarning("No quick save found!");
            return;
        }
        
        // Play sound
        if (audioSource != null && quickLoadSound != null)
            audioSource.PlayOneShot(quickLoadSound);
        
        // Load from quick save slot
        SaveManager.instance.LoadGame(quickSaveSlotIndex);
        
        Debug.Log($"Quick loaded from slot {quickSaveSlotIndex}!");
    }
    
    /// <summary>
    /// Show quick save notification (implement your own UI notification here)
    /// </summary>
    private void ShowQuickSaveNotification()
    {
        // TODO: Show a small notification like "Game Saved" that fades out
        // You can use a simple UI text with DOTween fade animation
        Debug.Log("QUICK SAVE NOTIFICATION - Game Saved!");
    }
    
    /// <summary>
    /// Load the most recent save (useful for "Continue" button on main menu)
    /// </summary>
    public void LoadMostRecentSave()
    {
        int mostRecentSlot = -1;
        System.DateTime mostRecentDate = System.DateTime.MinValue;
        
        int totalSlots = SaveManager.instance.GetTotalSaveSlots();
        
        for (int i = 0; i < totalSlots; i++)
        {
            if (SaveManager.instance.DoesSaveExist(i))
            {
                SaveSlotData slotData = SaveManager.instance.GetSaveSlotInfo(i);
                
                if (slotData.saveDate > mostRecentDate)
                {
                    mostRecentDate = slotData.saveDate;
                    mostRecentSlot = i;
                }
            }
        }
        
        if (mostRecentSlot >= 0)
        {
            SaveManager.instance.LoadGame(mostRecentSlot);
        }
        else
        {
            Debug.LogWarning("No save files found!");
        }
    }
    
    /// <summary>
    /// Check if any save files exist (useful for enabling/disabling "Continue" button)
    /// </summary>
    public bool DoAnySavesExist()
    {
        int totalSlots = SaveManager.instance.GetTotalSaveSlots();
        
        for (int i = 0; i < totalSlots; i++)
        {
            if (SaveManager.instance.DoesSaveExist(i))
                return true;
        }
        
        return false;
    }
}
