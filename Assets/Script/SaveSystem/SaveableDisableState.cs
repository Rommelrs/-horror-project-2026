using UnityEngine;

/// <summary>
/// Remembers that this GameObject was switched off during gameplay (e.g. by an OnPlayerTrigger event)
/// and switches it off again after a checkpoint/save load. Needs a UniqueID on the same object.
/// The state is stored in SaveManager's triggered-zone set, so it rolls back with checkpoints
/// exactly like every other saved trigger (disabled after the checkpoint -> active again on respawn).
/// </summary>
[RequireComponent(typeof(UniqueID))]
public class SaveableDisableState : MonoBehaviour, ISaveable
{
    UniqueID uniqueID;

    private void Awake()
    {
        uniqueID = GetComponent<UniqueID>();
    }

    private void Start()
    {
        ApplySavedState();
    }

    private void OnDisable()
    {
        // activeSelf is only false when this object itself was switched off. A parent being disabled
        // or the scene unloading leaves activeSelf true, so those don't get recorded.
        if (gameObject.activeSelf || SaveManager.instance == null || uniqueID == null)
            return;

        SaveManager.instance.RegisterTriggeredZone(uniqueID.ID);
    }

    public void Save(SaveData saveData) { }

    public void Load(SaveData saveData)
    {
        ApplySavedState();
    }

    void ApplySavedState()
    {
        if (SaveManager.instance != null && SaveManager.instance.IsZoneTriggered(uniqueID.ID))
            gameObject.SetActive(false);
    }
}
