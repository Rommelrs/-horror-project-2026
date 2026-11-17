using UnityEngine;

/// <summary>
/// Shows 'show' and hides 'hide' while a pickup has been picked up (SaveManager's picked-up list, so a checkpoint restore or
/// a loaded save from before the pickup puts everything back). Used for Block 1's main gate: once the knife handle is taken
/// the gate is buried under rubble, so the way out of Block 1 runs through the side doors into the bridge Runner's area.
/// Sits on an object that stays active; checked every frame (one HashSet lookup).
/// </summary>
public class PickupBlockade : MonoBehaviour
{
    [Tooltip("UniqueID of the pickup (its SaveablePickup).")]
    [SerializeField] string pickupId;
    [SerializeField] GameObject[] show = new GameObject[0];
    [SerializeField] GameObject[] hide = new GameObject[0];

    int state = -1;

    void Update()
    {
        bool picked = SaveManager.instance != null && !string.IsNullOrEmpty(pickupId) && SaveManager.instance.IsItemPickedUp(pickupId);
        int s = picked ? 1 : 0;
        if (s == state) return;
        state = s;
        foreach (var g in show) if (g != null) g.SetActive(picked);
        foreach (var g in hide) if (g != null) g.SetActive(!picked);
    }

    public bool Blocked => state == 1;
}
