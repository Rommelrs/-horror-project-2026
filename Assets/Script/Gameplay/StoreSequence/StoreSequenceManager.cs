using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Phase 1 of the store level: the player is let loose in a normal-feeling store. The dread sequence (OnSequenceStart)
/// begins once the player has
///   - interacted with / clearly seen enough of the key locations (cash register, fuse box, storage-room door), and
///   - shown that the entrance door works (opened it, or stepped outside and back),
/// or when the fallback timer runs out, whichever comes first.
/// </summary>
public class StoreSequenceManager : MonoBehaviour
{
    public static StoreSequenceManager instance;

    [Header("Key locations")]
    [Tooltip("Leave empty to use every DiscoverableLocation in the scene.")]
    [SerializeField] List<DiscoverableLocation> keyLocations = new List<DiscoverableLocation>();
    [Tooltip("How many of the key locations must have been interacted with or clearly seen (3 = all of them).")]
    [SerializeField] int requiredLocations = 2;

    [Header("Entrance door")]
    [SerializeField] bool requireEntranceTested = true;
    [Tooltip("The main door. Opening it successfully counts as 'the door works'. You can also call MarkEntranceTested() from a trigger outside.")]
    [SerializeField] DoorInteractable entranceDoor;

    [Header("Timing")]
    [Tooltip("Starts the sequence anyway after this many seconds of play (the clock stops while the game is paused or frozen by a subtitle).")]
    [SerializeField] float fallbackSeconds = 60f;
    [Tooltip("A short beat between the last requirement being met and the sequence starting.")]
    [SerializeField] float startDelay = 1.5f;

    [Header("Player position")]
    [Tooltip("The sequence only starts while the player is inside this zone (the store + storage room), so nobody gets locked out. Leave empty to ignore.")]
    [SerializeField] Collider playerInsideZone;

    [Header("Events")]
    public UnityEvent OnSequenceStart;

    public bool SequenceStarted { get; private set; }
    public bool EntranceTested { get; private set; }
    public float Elapsed { get; private set; }

    bool tracking;
    bool conditionsMetLogged;
    float conditionsMetAt = -1f;
    Camera cam;

    void Awake()
    {
        instance = this;
    }

    IEnumerator Start()
    {
        if (entranceDoor != null)
            entranceDoor.OnInteractedSuccess.AddListener(MarkEntranceTested);

        if (keyLocations == null || keyLocations.Count == 0)
            keyLocations = new List<DiscoverableLocation>(FindObjectsOfType<DiscoverableLocation>());

        // wait for the level to finish loading before the clock starts
        yield return null;
        while (LoadingHandler.instance && LoadingHandler.IsLoading())
            yield return null;

        tracking = true;
        Debug.Log("[StoreSequence] phase 1 started: " + keyLocations.Count + " key locations, need " + requiredLocations + ", entrance required " + requireEntranceTested + ", fallback " + fallbackSeconds + "s");
    }

    public void MarkEntranceTested()
    {
        if (EntranceTested) return;
        EntranceTested = true;
        Debug.Log("[StoreSequence] entrance door established as working");
    }

    public int DiscoveredCount()
    {
        int n = 0;
        foreach (var l in keyLocations) if (l != null && l.Discovered) n++;
        return n;
    }

    public bool ConditionsMet()
    {
        if (DiscoveredCount() < Mathf.Min(requiredLocations, keyLocations.Count)) return false;
        if (requireEntranceTested && !EntranceTested) return false;
        return true;
    }

    void Update()
    {
        if (!tracking || SequenceStarted) return;

        Elapsed += Time.deltaTime;

        if (cam == null) cam = Camera.main;
        foreach (var l in keyLocations)
            if (l != null && !l.Seen) l.TickSight(cam, Time.deltaTime);

        if (ConditionsMet())
        {
            if (conditionsMetAt < 0f)
            {
                conditionsMetAt = Elapsed;
                if (!conditionsMetLogged)
                {
                    conditionsMetLogged = true;
                    Debug.Log("[StoreSequence] requirements met after " + Elapsed.ToString("F1") + "s");
                }
            }
            if (Elapsed - conditionsMetAt >= startDelay && CanStartNow())
                StartSequence("requirements met");
        }
        else if (Elapsed >= fallbackSeconds && CanStartNow())
        {
            StartSequence("fallback timer (" + DiscoveredCount() + "/" + keyLocations.Count + " locations, entrance " + EntranceTested + ")");
        }
    }

    // don't start in the middle of a menu / subtitle / pause
    bool CanStartNow()
    {
        if (GameManager.IsPaused) return false;
        if (playerInsideZone != null && Player.instance != null && !playerInsideZone.bounds.Contains(Player.instance.transform.position)) return false;
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) return false;
        if (ItemInspectionHandler.instance != null && ItemInspectionHandler.instance.InspectionMenuIsActive()) return false;
        if (CashRegisterInteractable.instance != null && CashRegisterInteractable.instance.CashRegisterMenuIsActive()) return false;
        return true;
    }

    public void StartSequence(string reason)
    {
        if (SequenceStarted) return;
        SequenceStarted = true;
        Debug.Log("[StoreSequence] >>> SEQUENCE START: " + reason);
        OnSequenceStart?.Invoke();
    }

    [ContextMenu("Force start sequence")]
    void ForceStart()
    {
        StartSequence("forced from the inspector");
    }
}
