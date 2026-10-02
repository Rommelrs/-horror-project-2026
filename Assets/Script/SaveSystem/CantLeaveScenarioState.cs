using UnityEngine;

/// <summary>
/// Rebuilds the "can't leave the alley" scenario state after a checkpoint/save load.
/// The scenario toggles plain GameObjects (Doors, CantLeaveSubtitles) that the save system doesn't
/// track, so without this the lock was only re-applied by the player re-entering the trigger zone -
/// which could re-lock them after the knife handle was already collected.
/// Lives on the CantLeaveScenario root; expects children Trigger (with SaveableTrigger),
/// CheckItems (ObjectDisappearanceDetector), CantLeaveSubtitles, and a sibling called Doors.
/// </summary>
public class CantLeaveScenarioState : MonoBehaviour, ISaveable
{
    GameObject doors;
    GameObject cantLeaveSubtitles;
    SaveableTrigger saveableTrigger;
    ObjectDisappearanceDetector detector;

    private void Awake()
    {
        Transform trigger = transform.Find("Trigger");
        if (trigger != null) saveableTrigger = trigger.GetComponent<SaveableTrigger>();

        Transform checkItems = transform.Find("CheckItems");
        if (checkItems != null) detector = checkItems.GetComponent<ObjectDisappearanceDetector>();

        Transform subtitles = transform.Find("CantLeaveSubtitles");
        if (subtitles != null) cantLeaveSubtitles = subtitles.gameObject;

        if (transform.parent != null)
        {
            Transform doorsTransform = transform.parent.Find("Doors");
            if (doorsTransform != null) doors = doorsTransform.gameObject;
        }
    }

    private void Start()
    {
        ApplySavedState();
    }

    public void Save(SaveData saveData) { }

    public void Load(SaveData saveData)
    {
        ApplySavedState();
    }

    void ApplySavedState()
    {
        if (SaveManager.instance == null) return;

        if (HandleAlreadyCollected())
            SetLocked(false);
        else if (saveableTrigger != null && saveableTrigger.WasAlreadyTriggered())
            SetLocked(true);
    }

    bool HandleAlreadyCollected()
    {
        if (detector == null || detector.objectsToMonitor == null) return false;

        foreach (GameObject obj in detector.objectsToMonitor)
        {
            if (obj == null) return true;

            UniqueID id = obj.GetComponent<UniqueID>();
            if (id == null) id = obj.GetComponentInChildren<UniqueID>(true);

            if (id != null && SaveManager.instance.IsItemPickedUp(id.ID))
                return true;
        }

        return false;
    }

    void SetLocked(bool locked)
    {
        if (doors != null) doors.SetActive(!locked);
        if (cantLeaveSubtitles != null) cantLeaveSubtitles.SetActive(locked);
    }
}
