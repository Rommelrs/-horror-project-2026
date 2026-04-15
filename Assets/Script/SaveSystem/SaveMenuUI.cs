using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Save menu UI - displays save slots and handles save operations
/// </summary>
public class SaveMenuUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject saveMenuPanel;
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private Transform slotsContainer;
    [SerializeField] private SaveSlotUI slotPrefab;
    
    [Header("Confirmation Panel")]
    [SerializeField] private TMP_Text confirmationText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button backButton;
    
    [Header("Audio")]
    [SerializeField] private AudioClip selectSound;
    [SerializeField] private AudioClip confirmSound;
    [SerializeField] private AudioClip cancelSound;
    
    private List<SaveSlotUI> slotUIs = new List<SaveSlotUI>();
    private int selectedSlotIndex = -1;
    private AudioSource audioSource;
    private bool isMenuActive = false;
    
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        
        // Setup button listeners
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmSave);
        
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelSave);
        
        if (backButton != null)
            backButton.onClick.AddListener(OnBack);
    }
    
    /// <summary>
    /// Open the save menu
    /// </summary>
    public void OpenSaveMenu()
    {
        if (saveMenuPanel == null)
            Debug.LogError("[SaveMenuUI] saveMenuPanel is NULL!");
        else
            saveMenuPanel.SetActive(true);
        
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        
        RefreshSlots();
        GameManager.IsPaused = true;
        isMenuActive = true;
    }
    
    /// <summary>
    /// Close the save menu
    /// </summary>
    public void CloseSaveMenu()
    {
        if (saveMenuPanel != null)
            saveMenuPanel.SetActive(false);
        
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        
        selectedSlotIndex = -1;
        GameManager.IsPaused = false;
        isMenuActive = false;
    }
    
    /// <summary>
    /// Check if the save menu is currently active
    /// </summary>
    public bool IsMenuActive()
    {
        return isMenuActive;
    }
    
    /// <summary>
    /// Refresh all save slot displays
    /// </summary>
    private void RefreshSlots()
    {
        // Clear existing slots
        foreach (SaveSlotUI slot in slotUIs)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }
        slotUIs.Clear();
        
        if (SaveManager.instance == null)
            return;

        if (slotPrefab == null)
            return;

        if (slotsContainer == null)
            return;

        // Get slot data from SaveManager
        int totalSlots = SaveManager.instance.GetTotalSaveSlots();
        
        for (int i = 0; i < totalSlots; i++)
        {
            SaveSlotData slotData = SaveManager.instance.GetSaveSlotInfo(i);
            
            // Instantiate slot UI
            SaveSlotUI slotUI = Instantiate(slotPrefab, slotsContainer);
            slotUI.Initialize(slotData, OnSlotSelected);
            slotUIs.Add(slotUI);
        }
    }
    
    /// <summary>
    /// Called when a slot is selected
    /// </summary>
    private void OnSlotSelected(int slotIndex)
    {
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
    /// Show confirmation dialog for saving
    /// </summary>
    private void ShowConfirmation(int slotIndex)
    {
        if (confirmationPanel == null) return;
        
        SaveSlotData slotData = SaveManager.instance.GetSaveSlotInfo(slotIndex);
        
        if (confirmationText != null)
        {
            if (slotData.isEmpty)
            {
                confirmationText.text = $"Save game to Slot {slotIndex + 1}?";
            }
            else
            {
                confirmationText.text = $"Overwrite save in Slot {slotIndex + 1}?\n\n" +
                                       $"Previous Save:\n" +
                                       $"{slotData.sceneName}\n" +
                                       $"{slotData.GetFormattedDate()}\n" +
                                       $"Time: {slotData.GetFormattedPlaytime()}";
            }
        }
        
        confirmationPanel.SetActive(true);
    }
    
    /// <summary>
    /// Confirm save operation
    /// </summary>
    private void OnConfirmSave()
    {
        if (selectedSlotIndex < 0) return;
        
        // Play sound
        if (audioSource != null && confirmSound != null)
            audioSource.PlayOneShot(confirmSound);
        
        // Save game
        SaveManager.instance.SaveGame(selectedSlotIndex);
        
        // Show save success message (you can add a success panel here)
        Debug.Log($"Game saved to slot {selectedSlotIndex}!");
        
        // Close menu
        CloseSaveMenu();
    }
    
    /// <summary>
    /// Cancel save operation
    /// </summary>
    private void OnCancelSave()
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
    /// Go back to previous menu
    /// </summary>
    private void OnBack()
    {
        // Play sound
        if (audioSource != null && cancelSound != null)
            audioSource.PlayOneShot(cancelSound);
        
        CloseSaveMenu();
    }
}
