using System;
using UnityEngine;

/// <summary>
/// Metadata about a save slot - used for displaying save info in UI
/// </summary>
[System.Serializable]
public class SaveSlotData
{
    public int slotIndex;
    public bool isEmpty;
    public string sceneName;
    public DateTime saveDate;
    public float playtime;
    public int playerHealth;
    public int playerMaxHealth;
    
    public SaveSlotData(int slotIndex)
    {
        this.slotIndex = slotIndex;
        this.isEmpty = true;
        this.sceneName = "";
        this.saveDate = DateTime.Now;
        this.playtime = 0f;
        this.playerHealth = 0;
        this.playerMaxHealth = 0;
    }
    
    public string GetFormattedDate()
    {
        return saveDate.ToString("MM/dd/yyyy  HH:mm");
    }
    
    public string GetFormattedPlaytime()
    {
        TimeSpan timeSpan = TimeSpan.FromSeconds(playtime);
        return $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
    }
}
