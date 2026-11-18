using System.Collections;
using UnityEngine;

/// <summary>
/// On Block 1's street gate (DoorInteractable (Entrance)). Going through the gate INTO Block 1 fires the "can't leave yet" lock
/// (CantLeaveScenario/Trigger: the doors swap for "There might be something important before I leave" until the knife handle
/// is picked up) - user 2026-11-18: it has to happen when he passes the gate, not only when he walks over the strip 5 m in
/// (the gate drops him just inside, short of it, so going straight back out skipped the lock). The strip still works too.
/// Also the one answer to "has Khalid been into Block 1 yet?" (the park chase's back-to-the-gate reminder stops then).
/// Built by Tools > Cutscenes > Build Block 1 Gate Hooks.
/// </summary>
public class GateEntryLock : MonoBehaviour
{
    public const string EnteredFlag = "Block1Entered";
    static GateEntryLock instance;

    [SerializeField] DoorInteractable door;
    [Tooltip("CantLeaveScenario/Trigger (OnPlayerTrigger + SaveableTrigger).")]
    [SerializeField] OnPlayerTrigger lockTrigger;
    [Tooltip("UniqueID of the lock trigger (it destroys itself after firing, the save keeps the id).")]
    [SerializeField] string lockTriggerId;
    [Tooltip("UniqueID of the knife handle pickup: picked up = long past the lock.")]
    [SerializeField] string knifeHandleId;

    void Awake() { instance = this; }
    void OnDestroy() { if (instance == this) instance = null; }
    void OnEnable() { if (door != null) door.OnInteractedSuccess.AddListener(Passed); }
    void OnDisable() { if (door != null) door.OnInteractedSuccess.RemoveListener(Passed); }

    /// <summary>Khalid has been through the gate (or the lock fired, or he has the knife handle): true from then on, saves too.</summary>
    public static bool PastGate
    {
        get
        {
            if (StoryFlags.Has(EnteredFlag)) return true;
            var save = SaveManager.instance;
            if (instance == null || save == null) return false;
            return (!string.IsNullOrEmpty(instance.lockTriggerId) && save.IsZoneTriggered(instance.lockTriggerId))
                || (!string.IsNullOrEmpty(instance.knifeHandleId) && save.IsItemPickedUp(instance.knifeHandleId));
        }
    }

    void Passed()
    {
        StopAllCoroutines();
        StartCoroutine(Co_WaitForTheOtherSide());
    }

    /// <summary>The door fades and moves him: wait until he is on the inside (nearer its inside point than its outside one).</summary>
    IEnumerator Co_WaitForTheOtherSide()
    {
        if (door == null || door.exitPoint == null || door.enterPoint == null) yield break;
        for (float t = 0f; t < 5f; t += Time.unscaledDeltaTime)
        {
            var p = Player.instance;
            if (p != null)
            {
                Vector3 pos = p.transform.position;
                float dIn = Flat(pos - door.exitPoint.position), dOut = Flat(pos - door.enterPoint.position);
                if (dIn < 2.5f && dIn < dOut - 0.5f) { Inside(); yield break; }
            }
            yield return null;
        }
    }

    void Inside()
    {
        StoryFlags.Set(EnteredFlag);
        var save = SaveManager.instance;
        if (save != null && !string.IsNullOrEmpty(knifeHandleId) && save.IsItemPickedUp(knifeHandleId)) return;   // already free to go
        if (lockTrigger == null) return;                                                                         // fired already
        var saveable = lockTrigger.GetComponent<SaveableTrigger>();
        if (saveable != null && saveable.WasAlreadyTriggered()) return;
        lockTrigger.Trigger();
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
}
