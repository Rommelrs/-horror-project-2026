using UnityEngine;

/// <summary>
/// What a gate looks like (2026-11-25, playtesters couldn't tell them apart): chained while it needs a key, plated over on the side it
/// doesn't open from until it was opened from the other. Looks only; asks the door itself, so saves and checkpoints are right.
/// </summary>
public class DoorLook : MonoBehaviour
{
    [SerializeField] DoorInteractable door;
    [Tooltip("Shown until the door's key has been used.")]
    [SerializeField] GameObject whileLocked;
    [Tooltip("One-way doors: shown until it was opened from the right side.")]
    [SerializeField] GameObject untilOpened;
    float next;

    void Update()
    {
        if (door == null || Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.4f;
        if (whileLocked != null && whileLocked.activeSelf == door.IsUnlocked) whileLocked.SetActive(!door.IsUnlocked);
        if (untilOpened != null && untilOpened.activeSelf == door.HasBeenOpened) untilOpened.SetActive(!door.HasBeenOpened);
    }
}
