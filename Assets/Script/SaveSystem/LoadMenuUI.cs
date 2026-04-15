using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Load menu UI - displays save slots and handles load operations
/// </summary>
public class LoadMenuUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject loadMenuPanel;
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private Transform slotsContainer;
    [SerializeField] private SaveSlotUI slotPrefab;
    
    [Header("Confirmation Panel")]
    [SerializeField] private TMP_Text confirmationText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button backButton;
    
    [Header("Delete Confirmation")]
    [SerializeField] private GameObject deleteConfirmationPanel;
    [SerializeField] private TMP_Text deleteConfirmationText;
    [SerializeField] private Button confirmDeleteButton;
    [SerializeField] private Button cancelDeleteButton;
    
    [Header("Audio")]
    [SerializeField] private AudioClip selectSound;
    [SerializeField] private AudioClip confirmSound;
    [SerializeField] private AudioClip cancelSound;
    [SerializeField] private AudioClip deleteSound;
    
    private List<SaveSlotUI> slotUIs = new List<SaveSlotUI>();
    private int selectedSlotIndex = -1;
    private AudioSource audioSource;
    private CanvasGroup mainMenuCanvasGroup;
    
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        
        // Try to find the ButtonGroup specifically (not the whole Menu)
        GameObject buttonGroupObj = GameObject.Find("ButtonGroup");
        if (buttonGroupObj != null)
        {
            mainMenuCanvasGroup = buttonGroupObj.GetComponent<CanvasGroup>();
            if (mainMenuCanvasGroup == null)
            {
                // Add CanvasGroup if it doesn't exist
                mainMenuCanvasGroup = buttonGroupObj.AddComponent<CanvasGroup>();
            }
        }
        
        // Setup button listeners
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmLoad);
        
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelLoad);
        
        if (deleteButton != null)
            deleteButton.onClick.AddListener(OnDeleteButtonPressed);
        
        if (backButton != null)
            backButton.onClick.AddListener(OnBack);
        
        if (confirmDeleteButton != null)
            confirmDeleteButton.onClick.AddListener(OnConfirmDelete);
        
        if (cancelDeleteButton != null)
            cancelDeleteButton.onClick.AddListener(OnCancelDelete);
    }
    
    /// <summary>
    /// Open the load menu
    /// </summary>
    public void OpenLoadMenu()
    {
        if (loadMenuPanel != null)
        {
            loadMenuPanel.SetActive(true);
        }
        
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        
        if (deleteConfirmationPanel != null)
            deleteConfirmationPanel.SetActive(false);
        
        RefreshSlots();
        
        // Only set pause if we're in game, not in main menu
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene != "Menu")
        {
            GameManager.IsPaused = true;
        }
        else
        {
            // Keep cursor visible and unlocked in main menu
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        
        // Disable main menu interaction
        if (mainMenuCanvasGroup != null)
        {
            mainMenuCanvasGroup.interactable = false;
            mainMenuCanvasGroup.blocksRaycasts = false;
        }
    }
    
    /// <summary>
    /// Close the load menu
    /// </summary>
    public void CloseLoadMenu()
    {
        if (loadMenuPanel != null)
            loadMenuPanel.SetActive(false);
        
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        
        if (deleteConfirmationPanel != null)
            deleteConfirmationPanel.SetActive(false);
        
        selectedSlotIndex = -1;
        
        // Only unpause if we're not in the main menu
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene != "Menu")
        {
            GameManager.IsPaused = false;
        }
        
        // Re-enable main menu interaction
        if (mainMenuCanvasGroup != null)
        {
            mainMenuCanvasGroup.interactable = true;
            mainMenuCanvasGroup.blocksRaycasts = true;
        }
        
        // Make sure cursor stays visible in main menu
        if (currentScene == "Menu")
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
    
    /// <summary>
    /// Refresh all save slot displays
    /// </summary>
    private void RefreshSlots()
    {
        if (SaveManager.instance == null)
        {
            Debug.LogError("SaveManager instance not found! Cannot refresh slots.");
            return;
        }
        
        // Clear existing slots
        foreach (SaveSlotUI slot in slotUIs)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }
        slotUIs.Clear();
        
        // Get slot data from SaveManager
        int totalSlots = SaveManager.instance.GetTotalSaveSlots();
        
        for (int i = 0; i < totalSlots; i++)
        {
            SaveSlotData slotData = SaveManager.instance.GetSaveSlotInfo(i);
            
            // Instantiate slot UI
            SaveSlotUI slotUI = Instantiate(slotPrefab, slotsContainer);
            slotUI.Initialize(slotData, OnSlotSelected);
            
            // Only allow selecting filled slots for loading
            slotUI.SetInteractable(!slotData.isEmpty);
            
            slotUIs.Add(slotUI);
        }
    }
    
    /// <summary>
    /// Called when a slot is selected
    /// </summary>
    private void OnSlotSelected(int slotIndex)
    {
        SaveSlotData slotData = SaveManager.instance.GetSaveSlotInfo(slotIndex);
        
        // Can only select filled slots
        if (slotData.isEmpty) return;
        
        selectedSlotIndex = slotIndex;
        
        // Play sound
        if (audioSource != null && selectSound != null)
            audioSource.PlayOneShot(selectSound);
        
        // Update selection visuals
        foreach (SaveSlotUI slot in slotUIs)
        {
            slot.SetSelected(slot.GetSlotIndex() == slotIndex);
        }
        
        // Show confirmation panel
        ShowConfirmation(slotIndex);
    }
    
    /// <summary>
    /// Show confirmation dialog for loading
    /// </summary>
    private void ShowConfirmation(int slotIndex)
    {
        if (confirmationPanel == null) return;
        
        SaveSlotData slotData = SaveManager.instance.GetSaveSlotInfo(slotIndex);
        
        if (confirmationText != null)
        {
            confirmationText.text = $"Load Slot {slotIndex + 1}?\\n\\n" +
                                   $"{slotData.sceneName}\\n" +
                                   $"{slotData.GetFormattedDate()}\\n" +
                                   $"Time: {slotData.GetFormattedPlaytime()}\\n" +
                                   $"HP: {slotData.playerHealth}/{slotData.playerMaxHealth}\\n\\n" +
                                   $"Unsaved progress will be lost!";
        }
        
        confirmationPanel.SetActive(true);
    }
    
    /// <summary>
    /// Confirm load operation
    /// </summary>
    private void OnConfirmLoad()
    {
        if (selectedSlotIndex < 0) return;
        
        // Play sound
        if (audioSource != null && confirmSound != null)
            audioSource.PlayOneShot(confirmSound);
        
        // Load game
        SaveManager.instance.LoadGame(selectedSlotIndex);
        
        // Menu will close automatically after scene loads
    }
    
    /// <summary>
    /// Cancel load operation
    /// </summary>
    private void OnCancelLoad()
    {
        // Play sound
        if (audioSource != null && cancelSound != null)
            audioSource.PlayOneShot(cancelSound);
        
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        
        selectedSlotIndex = -1;
        
        // Deselect all slots
        foreach (SaveSlotUI slot in slotUIs)
        {
            slot.SetSelected(false);
        }
    }
    
    /// <summary>
    /// Delete button pressed - show delete confirmation
    /// </summary>
    private void OnDeleteButtonPressed()
    {
        if (selectedSlotIndex < 0) return;
        
        SaveSlotData slotData = SaveManager.instance.GetSaveSlotInfo(selectedSlotIndex);
        if (slotData.isEmpty) return;
        
        // Play sound
        if (audioSource != null && selectSound != null)
            audioSource.PlayOneShot(selectSound);
        
        if (deleteConfirmationPanel != null)
        {
            if (deleteConfirmationText != null)
            {
                deleteConfirmationText.text = $"Delete save in Slot {selectedSlotIndex + 1}?\\n\\n" +
                                             $"This action cannot be undone!";
            }
            
            deleteConfirmationPanel.SetActive(true);
        }
    }
    
    /// <summary>
    /// Confirm delete operation
    /// </summary>
    private void OnConfirmDelete()
    {
        if (selectedSlotIndex < 0) return;
        
        // Play sound
        if (audioSource != null && deleteSound != null)
            audioSource.PlayOneShot(deleteSound);
        
        // Delete save
        SaveManager.instance.DeleteSave(selectedSlotIndex);
        
        if (deleteConfirmationPanel != null)
            deleteConfirmationPanel.SetActive(false);
        
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        
        selectedSlotIndex = -1;
        
        // Refresh slots
        RefreshSlots();
    }
    
    /// <summary>
    /// Cancel delete operation
    /// </summary>
    private void OnCancelDelete()
    {
        // Play sound
        if (audioSource != null && cancelSound != null)
            audioSource.PlayOneShot(cancelSound);
        
        if (deleteConfirmationPanel != null)
            deleteConfirmationPanel.SetActive(false);
    }
    
    /// <summary>
    /// Go back to previous menu
    /// </summary>
    private void OnBack()
    {
        // Play sound
        if (audioSource != null && cancelSound != null)
            audioSource.PlayOneShot(cancelSound);
        
        CloseLoadMenu();
    }
}
