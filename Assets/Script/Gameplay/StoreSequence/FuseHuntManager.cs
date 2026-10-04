using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;

/// <summary>
/// Runs the fuse hunt in the store.
///  - Before the player has identified the problem (inspected the fuse box AND the cash register), searching a cabinet only
///    gives "Nothing useful." and does not count.
///  - After that, every cabinet the player searches for the first time counts as searched. The fuse is placed in the LAST
///    cabinet they search, so it is always in the final place they look.
///  - Opening a cabinet that has no fuse says one of the "no fuse here" lines.
/// </summary>
public class FuseHuntManager : MonoBehaviour
{
    public static FuseHuntManager instance;

    [Header("Hunt")]
    [Tooltip("On: searches only count once BeginHunt() has been called. Off (normal): they count once the problem is identified (cash register + fuse box inspected, or the store sequence started). Before that every cabinet says 'Nothing useful'.")]
    [SerializeField] bool requireHuntBegin = false;
    [Tooltip("When the director takes care of the reveal, the fuse stays hidden after the last search until RevealPendingFuse() is called (so the storage-room sound comes first).")]
    public bool revealIsHandledByDirector;
    [Tooltip("The fuse appears by itself this long after the last search if nobody revealed it.")]
    [SerializeField] float revealFailsafeSeconds = 20f;

    [Header("Problem identified when (only used when 'Require Hunt Begin' is off)")]
    [Tooltip("All of these must have been interacted with (fuse box + cash register).")]
    public DiscoverableLocation[] problemLocations;
    [Tooltip("Also counts as identified the moment the store sequence starts (the power cut makes the problem obvious).")]
    public bool identifyWhenSequenceStarts = true;

    [Header("Fuse")]
    [Tooltip("The fuse pickup that is in the scene. It is hidden at the start and moved into the last cabinet that is searched.")]
    public GameObject fuseObject;

    [Header("Lines (Subtitles table)")]
    public LocalizedString[] nothingUsefulLines;
    public LocalizedString[] noFuseHereLines;
    [Tooltip("Subtitles normally freeze the game; for these short remarks keep the game running.")]
    public bool freezeGameOnSubtitle = false;

    [Header("Events")]
    public UnityEvent onProblemIdentified;
    public UnityEvent onFuseRevealed;
    [Tooltip("Fires the moment the player picks the fuse up.")]
    public UnityEvent onFusePickedUp;

    /// <summary>(searches so far, total cabinets, the cabinet just searched). Fires once per cabinet, the first time it counts.</summary>
    public event System.Action<int, int, SearchableCabinet> SearchCounted;

    public bool HuntActive { get; private set; }
    public bool FinalSearchDone { get; private set; }
    public int SearchedCount { get { return searched.Count; } }
    public int TotalCabinets { get { return CountActiveCabinets(); } }

    public bool FuseRevealed { get; private set; }
    public bool FusePickedUp { get; private set; }
    public SearchableCabinet FuseCabinet { get; private set; }

    readonly HashSet<SearchableCabinet> searched = new HashSet<SearchableCabinet>();
    bool forcedIdentified;
    bool identifiedAnnounced;
    int lastNothingIdx = -1, lastNoFuseIdx = -1;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        if (fuseObject != null)
        {
            var pickup = fuseObject.GetComponent<InspectableItemPickup>();
            if (pickup != null) pickup.OnInteracted.AddListener(OnFuseTaken);
            if (!FuseRevealed) fuseObject.SetActive(false);
        }

        if (identifyWhenSequenceStarts && StoreSequenceManager.instance != null)
            StoreSequenceManager.instance.OnSequenceStart.AddListener(ForceProblemIdentified);
    }

    void Update()
    {
        if (!identifiedAnnounced && ProblemIdentified)
        {
            identifiedAnnounced = true;
            Debug.Log("[FuseHunt] problem identified - cabinets now count as searched");
            onProblemIdentified?.Invoke();
        }
    }

    public void BeginHunt()
    {
        if (HuntActive) return;
        HuntActive = true;
        Debug.Log("[FuseHunt] hunt begins - the three cabinets now count");
    }

    public bool ProblemIdentified
    {
        get
        {
            if (requireHuntBegin) return HuntActive;
            if (forcedIdentified) return true;
            if (problemLocations == null || problemLocations.Length == 0) return false;
            foreach (var l in problemLocations)
                if (l == null || !l.Interacted) return false;
            return true;
        }
    }

    public void ForceProblemIdentified()
    {
        forcedIdentified = true;
    }

    void OnFuseTaken()
    {
        if (FusePickedUp) return;
        FusePickedUp = true;
        Debug.Log("[FuseHunt] fuse picked up");
        onFusePickedUp?.Invoke();
    }

    bool FuseCollected()
    {
        if (!FuseRevealed) return false;
        if (fuseObject == null) return true;
        return Player.instance != null && Player.instance.inventory != null && Player.instance.inventory.HasFuse();
    }

    /// <summary>Called by a SearchableCabinet every time the player opens it.</summary>
    public void NotifyOpened(SearchableCabinet cabinet)
    {
        if (!ProblemIdentified)
        {
            // player is poking around without knowing what they are looking for yet
            Say(nothingUsefulLines, ref lastNothingIdx);
            Debug.Log("[FuseHunt] '" + cabinet.displayName + "' opened before the problem was identified -> nothing useful (not counted)");
            return;
        }

        bool isNew = searched.Add(cabinet);
        if (isNew && !FuseRevealed) SearchCounted?.Invoke(searched.Count, CountActiveCabinets(), cabinet);

        if (FuseRevealed)
        {
            if (cabinet == FuseCabinet && !FuseCollected())
                return; // the fuse is right there, no remark needed
            if (FuseCollected()) Say(nothingUsefulLines, ref lastNothingIdx);
            else Say(noFuseHereLines, ref lastNoFuseIdx);
            return;
        }

        bool allSearched = true;
        foreach (var c in SearchableCabinet.All)
            if (c != null && c.isActiveAndEnabled && !searched.Contains(c)) { allSearched = false; break; }

        if (allSearched)
        {
            if (pendingCabinet != null) return;   // the fuse is already on its way
            FinalSearchDone = true;
            if (revealIsHandledByDirector)
            {
                pendingCabinet = cabinet;
                StartCoroutine(Co_RevealFailsafe());
                Debug.Log("[FuseHunt] last cabinet '" + cabinet.displayName + "' opened - fuse reveal is waiting for the director");
            }
            else RevealFuse(cabinet);
        }
        else
        {
            Say(noFuseHereLines, ref lastNoFuseIdx);
            Debug.Log("[FuseHunt] '" + cabinet.displayName + "' searched (" + searched.Count + "/" + CountActiveCabinets() + ") - no fuse here");
        }
    }

    SearchableCabinet pendingCabinet;

    IEnumerator Co_RevealFailsafe()
    {
        yield return new WaitForSeconds(revealFailsafeSeconds);
        RevealPendingFuse();
    }

    /// <summary>Puts the fuse into the last cabinet that was searched (called by the director once the sound cue has played).</summary>
    public void RevealPendingFuse()
    {
        if (pendingCabinet == null || FuseRevealed) return;
        var c = pendingCabinet;
        pendingCabinet = null;
        RevealFuse(c);
    }

    int CountActiveCabinets()
    {
        int n = 0;
        foreach (var c in SearchableCabinet.All) if (c != null && c.isActiveAndEnabled) n++;
        return n;
    }

    void RevealFuse(SearchableCabinet cabinet)
    {
        FuseRevealed = true;
        FuseCabinet = cabinet;

        if (fuseObject != null)
        {
            Transform slot = cabinet.fuseSlot != null ? cabinet.fuseSlot : cabinet.transform;
            fuseObject.transform.SetParent(slot, false);
            fuseObject.transform.localPosition = Vector3.zero;
            fuseObject.transform.localRotation = Quaternion.identity;
            fuseObject.SetActive(true);
            cabinet.RefreshSlotContents();
        }

        Debug.Log("[FuseHunt] last cabinet searched = '" + cabinet.displayName + "' -> fuse placed there");
        onFuseRevealed?.Invoke();
    }

    void Say(LocalizedString[] pool, ref int lastIndex)
    {
        if (pool == null || pool.Length == 0) return;
        int idx = Random.Range(0, pool.Length);
        if (pool.Length > 1 && idx == lastIndex) idx = (idx + 1) % pool.Length;
        lastIndex = idx;
        if (pool[idx] == null || pool[idx].IsEmpty) return;
        SubtitleManager.ShowSubtitle(pool[idx], freezeGameOnSubtitle);
    }
}
