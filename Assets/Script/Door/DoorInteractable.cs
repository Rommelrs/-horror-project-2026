using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;

public class DoorInteractable : MonoBehaviour
{
    public Animator doorAnim;
    public Transform interactPoint;
    public Transform enterPoint;
    public Transform exitPoint;

    [Header("Auto Interact")]
    [Tooltip("On: the door is used the moment the player walks into one of its DoorInteractableTriggers, no E press needed (keys / locks still apply).")]
    public bool autoInteractOnEnter = false;

    public bool requiresKey = false;
    public int keyCodeRequired;

    [Header("Unlock Step (needs 'Requires Key')")]
    [Tooltip("Off: having the key just lets the door open. On: without the key the door says it is locked; with the key the first E only unlocks it (sound + 'It's unlocked'), and a second E goes through. The unlocked state is saved: this object needs a UniqueID and a SaveableInteractable.")]
    public bool unlockStepWithKey = false;
    [Tooltip("Shown when the player tries the door without the key.")]
    public LocalizedString lockedLine;
    [Tooltip("Shown when the key unlocks the door.")]
    public LocalizedString unlockedLine;
    [Tooltip("Played when the key unlocks the door.")]
    public AudioClip unlockClip;
    [Tooltip("Subtitles normally freeze the game; for these short remarks keep it running.")]
    public bool freezeGameOnSubtitle = false;
    [Tooltip("Fires when the key unlocks the door (not when a saved unlocked door is loaded).")]
    public UnityEvent OnUnlocked;

    [Header("One-way (shortcut doors)")]
    [Tooltip("On: from the 'Blocked Side' the door says it does not open from this side - until the player has opened it once from the other side. From then on it works from both sides (like a shortcut in the Souls games). The opened state is saved: this object needs a UniqueID.")]
    public bool oneWayUntilOpened = false;
    [Tooltip("The wrong side. Enter = the side of the door's 'Enter' trigger (the player gets sent to the Exit point), Exit = the side of the 'Exit' trigger.")]
    public DoorTriggerType blockedSide = DoorTriggerType.Exit;
    [Tooltip("Shown when the player tries the door from the blocked side.")]
    public LocalizedString wrongSideLine;

    public bool hasDoorLock = false;
    public Lock doorLock;

    [Header("Locked Subtitle")]
    public SubtitleTrigger lockedSubtitleTrigger;
    public Item requiredItemForSubtitle;

    [Header("Custom Sounds (Optional)")]
    public AudioClip customOpenClip;
    public AudioClip customCloseClip;
    public AudioClip customLockedClip;

    public UnityEvent OnInteracted;
    public UnityEvent OnInteractedSuccess;
    public UnityEvent OnInteractionFailed;

    /// <summary>True once the key has been used on this door (only used with 'Unlock Step With Key'). Restored from the save.</summary>
    public bool IsUnlocked { get; private set; }

    /// <summary>True once a one-way door was opened from its correct side (only used with 'One Way Until Opened'). Restored from the save.</summary>
    public bool HasBeenOpened { get; private set; }

    SaveableInteractable saveable;
    UniqueID uniqueID;

    void Awake()
    {
        saveable = GetComponent<SaveableInteractable>();
        uniqueID = GetComponent<UniqueID>();
    }

    void Start()
    {
        // already unlocked in a save / checkpoint that was loaded (the save data is restored before Start runs)
        if (unlockStepWithKey && saveable != null && saveable.WasAlreadyUsed())
            IsUnlocked = true;
        if (oneWayUntilOpened && uniqueID != null && SaveManager.instance != null && SaveManager.instance.IsInteractableUsed(OpenedSaveKey))
            HasBeenOpened = true;
    }

    // its own save key, so it never clashes with the key-unlock flag that SaveableInteractable keeps under the plain ID
    string OpenedSaveKey => uniqueID.ID + "_opened";

    /// <summary>One-way doors: the player just went through from the correct side, so the door now opens from both sides (saved).</summary>
    public void MarkOpenedFromCorrectSide()
    {
        if (!oneWayUntilOpened || HasBeenOpened) return;
        HasBeenOpened = true;
        if (uniqueID != null && SaveManager.instance != null) SaveManager.instance.RegisterUsedInteractable(OpenedSaveKey);
        else Debug.LogWarning("[Door] '" + name + "' is one-way but has no UniqueID: the opened state will not be saved.", this);
    }

    /// <summary>True when the player is at the blocked side of a one-way door that has not been opened yet.</summary>
    public bool IsBlockedFrom(DoorTriggerType side)
    {
        return oneWayUntilOpened && !HasBeenOpened && side == blockedSide;
    }

    public void Unlock()
    {
        if (IsUnlocked) return;
        IsUnlocked = true;
        if (saveable != null) saveable.MarkAsUsed();   // remembered by the save system
        else Debug.LogWarning("[Door] '" + name + "' has no SaveableInteractable: its unlocked state will not be saved.", this);
        OnUnlocked?.Invoke();
    }

    public void ShowLockedLine() { Say(lockedLine); }
    public void ShowUnlockedLine() { Say(unlockedLine); }
    public void ShowWrongSideLine() { Remark(wrongSideLine); }

    void Say(LocalizedString line)
    {
        if (!unlockStepWithKey) return;
        Remark(line);
    }

    void Remark(LocalizedString line)
    {
        if (line == null || line.IsEmpty) return;
        // The subtitle's "continue" button is the same E as the door's: without this stamp the very press that opens the subtitle
        // would also close it in the same frame (what every other interaction does before it shows a subtitle).
        if (Player.instance != null) Player.instance.SetLastInteractionTime(Time.unscaledTime);
        if (DoorInteractionHandler.instance != null) DoorInteractionHandler.instance.NoteDoorRemark();
        SubtitleManager.ShowSubtitle(line, freezeGameOnSubtitle);
    }

    public bool ShouldTriggerLockedSubtitle()
    {
        if (!hasDoorLock || lockedSubtitleTrigger == null || requiredItemForSubtitle == null)
            return false;

        if (Player.instance == null || Player.instance.inventory == null)
            return false;

        foreach (var stack in Player.instance.inventory.GetItems())
        {
            if (stack.item == requiredItemForSubtitle)
                return true;
        }

        return false;
    }
}
