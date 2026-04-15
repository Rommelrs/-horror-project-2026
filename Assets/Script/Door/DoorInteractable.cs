using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class DoorInteractable : MonoBehaviour
{
    public Animator doorAnim;
    public Transform interactPoint;
    public Transform enterPoint;
    public Transform exitPoint;

    public bool requiresKey = false;
    public int keyCodeRequired;

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
