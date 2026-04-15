using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// UI component for a single save slot - displays save info and handles selection
/// </summary>
public class SaveSlotUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text slotNumberText;
    [SerializeField] private TMP_Text sceneNameText;
    [SerializeField] private TMP_Text saveDateText;
    [SerializeField] private TMP_Text playtimeText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private GameObject emptySlotPanel;
    [SerializeField] private GameObject filledSlotPanel;
    [SerializeField] private Image thumbnailImage; // Optional
    
    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Image selectionHighlight;
    
    private SaveSlotData slotData;
    private Button button;
    private bool isSelected = false;
    
    private void Awake()
    {
        button = GetComponent<Button>();
    }
    
    /// <summary>
    /// Initialize the slot with save data
    /// </summary>
    public void Initialize(SaveSlotData data, Action<int> onSlotSelected)
    {
        slotData = data;
        
        // Setup button callback
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSlotSelected?.Invoke(slotData.slotIndex));
        }
        
        UpdateDisplay();
    }
    
    /// <summary>
    /// Update the visual display based on slot data
    /// </summary>
    public void UpdateDisplay()
    {
        if (slotData == null) return;
        
        // Update slot number
        if (slotNumberText != null)
            slotNumberText.text = $"SLOT {slotData.slotIndex + 1}";
        
        if (slotData.isEmpty)
        {
            // Show empty slot UI
            if (emptySlotPanel != null) emptySlotPanel.SetActive(true);
            if (filledSlotPanel != null) filledSlotPanel.SetActive(false);
        }
        else
        {
            // Show filled slot UI
            if (emptySlotPanel != null) emptySlotPanel.SetActive(false);
            if (filledSlotPanel != null) filledSlotPanel.SetActive(true);
            
            // Update text fields
            if (sceneNameText != null)
                sceneNameText.text = slotData.sceneName;
            
            if (saveDateText != null)
                saveDateText.text = slotData.GetFormattedDate();
            
            if (playtimeText != null)
                playtimeText.text = $"Time: {slotData.GetFormattedPlaytime()}";
            
            if (healthText != null)
                healthText.text = $"HP: {slotData.playerHealth}/{slotData.playerMaxHealth}";
        }
    }
    
    /// <summary>
    /// Set the selection state of this slot
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        
        if (selectionHighlight != null)
        {
            selectionHighlight.enabled = selected;
            selectionHighlight.color = selected ? selectedColor : normalColor;
        }
    }
    
    /// <summary>
    /// Enable or disable interaction with this slot
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }
    
    public SaveSlotData GetSlotData()
    {
        return slotData;
    }
    
    public int GetSlotIndex()
    {
        return slotData != null ? slotData.slotIndex : -1;
    }
}
