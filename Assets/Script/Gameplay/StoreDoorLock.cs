using UnityEngine;

/// <summary>
/// On the Game scene's store door. Once Khalid owns the Hammer the store is done with for good: going back in soft-locked
/// playtesters (user 2026-11-25). The door's triggers are switched off - no prompt, no E - whatever brought him here
/// (walking, checkpoint, save load), because it asks the inventory itself every moment instead of remembering a state.
/// LoadButton.LoadGame refuses the Store scene too while he owns it in Game.
/// </summary>
public class StoreDoorLock : MonoBehaviour
{
    [SerializeField] DoorInteractable door;
    [SerializeField] float recheckSeconds = 0.25f;

    Collider[] triggers;
    float nextCheck;
    bool locked;

    /// <summary>The store must not be entered any more.</summary>
    public static bool Closed => PlayerMeleeWeapon.OwnsPipe;

    void Awake()
    {
        if (door == null) door = GetComponent<DoorInteractable>();
        triggers = GetComponentsInChildren<Collider>(true);
    }

    void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + recheckSeconds;
        bool closed = Closed;
        // off every time (the door's own events may have switched an object back on); back on only if he doesn't own it any more
        if (closed || locked)
            foreach (var c in triggers)
                if (c != null && c.isTrigger && c.enabled == closed) c.enabled = !closed;
        if (closed && !locked && DoorInteractionHandler.instance != null)
            DoorInteractionHandler.instance.DoorInteractionTriggerExit(door);      // he may be standing in it
        locked = closed;
    }
}
