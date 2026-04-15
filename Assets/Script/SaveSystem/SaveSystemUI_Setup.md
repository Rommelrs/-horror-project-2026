# Save System UI Setup Guide

## Overview
The save system now has a complete UI with save/load menus, slot selection, confirmation dialogs, and quick save/load functionality.

## Scripts Created

### Core UI Scripts
1. **SaveSlotData.cs** - Metadata structure for save slot info
2. **SaveSlotUI.cs** - Individual save slot display component
3. **SaveMenuUI.cs** - Save menu with slot selection and confirmation
4. **LoadMenuUI.cs** - Load menu with slot selection, confirmation, and delete
5. **SaveLoadMenuManager.cs** - Global manager for accessing save/load menus
6. **SavePoint.cs** (updated) - Opens SaveMenuUI when interacted with

### SaveManager Updates
- Added `GetSaveSlotInfo(int slotIndex)` method that returns `SaveSlotData`
- Returns formatted slot info (isEmpty, sceneName, saveDate, playtime, health)

---

## UI Setup Instructions

### 1. Create Save Slot Prefab

Create a prefab for individual save slots:

**GameObject Hierarchy:**
```
SaveSlot (GameObject + SaveSlotUI component + Button)
├── SelectionHighlight (Image - yellow border, disabled by default)
├── SlotNumber (TextMeshPro - "SLOT 1")
├── EmptySlotPanel (GameObject)
│   └── EmptyText (TextMeshPro - "Empty Slot")
└── FilledSlotPanel (GameObject, starts inactive)
    ├── SceneName (TextMeshPro - "Store")
    ├── SaveDate (TextMeshPro - "03/17/2026 23:30")
    ├── Playtime (TextMeshPro - "Time: 01:23:45")
    └── Health (TextMeshPro - "HP: 100/100")
```

**SaveSlotUI Inspector:**
- **Slot Number Text**: Assign SlotNumber TextMeshPro
- **Scene Name Text**: Assign SceneName TextMeshPro
- **Save Date Text**: Assign SaveDate TextMeshPro
- **Playtime Text**: Assign Playtime TextMeshPro
- **Health Text**: Assign Health TextMeshPro
- **Empty Slot Panel**: Assign EmptySlotPanel GameObject
- **Filled Slot Panel**: Assign FilledSlotPanel GameObject
- **Selection Highlight**: Assign SelectionHighlight Image
- **Normal Color**: White
- **Selected Color**: Yellow

---

### 2. Create Save Menu UI

**GameObject Hierarchy:**
```
SaveMenuPanel (GameObject + SaveMenuUI + AudioSource)
├── Background (Image - semi-transparent black)
├── Title (TextMeshPro - "SAVE GAME")
├── SlotsContainer (Vertical Layout Group)
│   └── (SaveSlot prefabs instantiated here at runtime)
├── ConfirmationPanel (GameObject, starts inactive)
│   ├── Background (Image)
│   ├── ConfirmationText (TextMeshPro - "Save to Slot 1?")
│   ├── ConfirmButton (Button + TextMeshPro - "CONFIRM")
│   └── CancelButton (Button + TextMeshPro - "CANCEL")
└── BackButton (Button + TextMeshPro - "BACK")
```

**SaveMenuUI Inspector:**
- **Save Menu Panel**: Assign SaveMenuPanel
- **Confirmation Panel**: Assign ConfirmationPanel
- **Slots Container**: Assign SlotsContainer
- **Slot Prefab**: Assign your SaveSlot prefab
- **Confirmation Text**: Assign ConfirmationText
- **Confirm Button**: Assign ConfirmButton
- **Cancel Button**: Assign CancelButton
- **Back Button**: Assign BackButton
- **Audio**: Assign AudioSource and sound clips (optional)

---

### 3. Create Load Menu UI

**GameObject Hierarchy:**
```
LoadMenuPanel (GameObject + LoadMenuUI + AudioSource)
├── Background (Image - semi-transparent black)
├── Title (TextMeshPro - "LOAD GAME")
├── SlotsContainer (Vertical Layout Group)
│   └── (SaveSlot prefabs instantiated here at runtime)
├── ConfirmationPanel (GameObject, starts inactive)
│   ├── Background (Image)
│   ├── ConfirmationText (TextMeshPro)
│   ├── ConfirmButton (Button + TextMeshPro - "LOAD")
│   ├── CancelButton (Button + TextMeshPro - "CANCEL")
│   └── DeleteButton (Button + TextMeshPro - "DELETE")
├── DeleteConfirmationPanel (GameObject, starts inactive)
│   ├── Background (Image)
│   ├── DeleteConfirmationText (TextMeshPro - "Delete save?")
│   ├── ConfirmDeleteButton (Button + TextMeshPro - "DELETE")
│   └── CancelDeleteButton (Button + TextMeshPro - "CANCEL")
└── BackButton (Button + TextMeshPro - "BACK")
```

**LoadMenuUI Inspector:**
- **Load Menu Panel**: Assign LoadMenuPanel
- **Confirmation Panel**: Assign ConfirmationPanel
- **Slots Container**: Assign SlotsContainer
- **Slot Prefab**: Assign your SaveSlot prefab
- **Confirmation Text**: Assign ConfirmationText
- **Confirm Button**: Assign ConfirmButton
- **Cancel Button**: Assign CancelButton
- **Delete Button**: Assign DeleteButton
- **Back Button**: Assign BackButton
- **Delete Confirmation Panel**: Assign DeleteConfirmationPanel
- **Delete Confirmation Text**: Assign DeleteConfirmationText
- **Confirm Delete Button**: Assign ConfirmDeleteButton
- **Cancel Delete Button**: Assign CancelDeleteButton
- **Audio**: Assign AudioSource and sound clips (optional)

---

### 4. Setup SaveLoadMenuManager

Create a GameObject called "SaveLoadMenuManager" with the SaveLoadMenuManager component:

**Inspector:**
- **Save Menu UI**: Assign your SaveMenuUI
- **Load Menu UI**: Assign your LoadMenuUI
- **Enable Quick Save Load**: Check if you want F5/F9 quick save/load
- **Quick Save Slot Index**: 0 (uses slot 0 for quick saves)
- **Quick Save Input**: Create Input Action for F5 key
- **Quick Load Input**: Create Input Action for F9 key
- **Audio**: Assign AudioSource and sound clips (optional)

This object should be in your main scene or set to DontDestroyOnLoad.

---

### 5. Update SavePoint

For each SavePoint in your scene:

**Inspector:**
- **Save Menu UI**: Assign your SaveMenuUI reference

When the player interacts with the SavePoint, it will open the SaveMenuUI.

---

### 6. Add to Pause Menu (Optional)

If you have a pause menu, add buttons that call:
- `SaveLoadMenuManager.instance.OpenSaveMenu()` - Opens save menu
- `SaveLoadMenuManager.instance.OpenLoadMenu()` - Opens load menu

---

### 7. Add to Main Menu (Optional)

On your main menu, add buttons:
- **Continue**: Calls `SaveLoadMenuManager.instance.LoadMostRecentSave()`
- **Load Game**: Calls `SaveLoadMenuManager.instance.OpenLoadMenu()`

You can enable/disable the Continue button based on:
```csharp
bool hasSaves = SaveLoadMenuManager.instance.DoAnySavesExist();
continueButton.interactable = hasSaves;
```

---

## Features

### Save Menu
- ✅ Displays all 5 save slots
- ✅ Shows empty vs filled slots
- ✅ Displays save info: Scene, Date/Time, Playtime, Health
- ✅ Confirmation dialog before saving
- ✅ Warning when overwriting existing save
- ✅ Visual selection highlight
- ✅ Sound effects (optional)

### Load Menu
- ✅ Displays all save slots (only filled ones are selectable)
- ✅ Shows detailed save info
- ✅ Confirmation dialog before loading
- ✅ Warning about losing unsaved progress
- ✅ Delete save functionality with confirmation
- ✅ Visual selection highlight
- ✅ Sound effects (optional)

### Quick Save/Load
- ✅ F5 to quick save (slot 0)
- ✅ F9 to quick load (slot 0)
- ✅ Works during gameplay (not in menus/cutscenes)
- ✅ Sound feedback

### Continue Feature
- ✅ Loads most recent save automatically
- ✅ Checks if any saves exist
- ✅ Perfect for main menu "Continue" button

---

## Testing

1. **Test Save Points**: Interact with SavePoint, select slot, save game
2. **Test Pause Menu Save**: Open pause menu, click save, select slot
3. **Test Load Menu**: Open load menu, select save, confirm load
4. **Test Delete**: Select save, click delete, confirm deletion
5. **Test Quick Save**: Press F5 during gameplay
6. **Test Quick Load**: Press F9 during gameplay
7. **Test Continue**: From main menu, click continue

---

## Customization

### Colors
- Edit `normalColor` and `selectedColor` in SaveSlotUI
- Customize panel backgrounds and text colors

### Layout
- Adjust Vertical Layout Group spacing in SlotsContainer
- Resize slot prefab for different visual styles

### Sounds
- Add your own audio clips for select, confirm, cancel, delete
- Assign to SaveMenuUI, LoadMenuUI, and SaveLoadMenuManager

### Text Formatting
- Edit `GetFormattedDate()` and `GetFormattedPlaytime()` in SaveSlotData.cs
- Change time format or date display style

---

## Notes

- Save slots are 0-indexed internally but displayed as 1-indexed to players (SLOT 1-5)
- Slot 0 is recommended for quick saves
- Empty slots can be saved to, filled slots show overwrite warning
- Only filled slots can be loaded from
- Delete requires double confirmation to prevent accidents
- All menus pause the game automatically

---

Enjoy your awesome save system! 🎮💾
