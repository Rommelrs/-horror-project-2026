using UnityEngine;

/// <summary>
/// Saves which map markers (child UI images) have been revealed.
/// IMPORTANT: Put this on an ALWAYS-ACTIVE GameObject (e.g. GameManager, EventSystem, or a dedicated MapMarkerManager)
/// NOT on the map UI parent (which may be inactive)
/// Each marker is recorded in the save system's progress (SaveManager's used-interactables, as "mapmarker:&lt;id&gt;"), so it
/// belongs to the save slot / checkpoint it was revealed in: continuing from an earlier checkpoint or loading an older slot
/// shows exactly the markers you had then, and New Game clears them with the rest of the progress. (They used to live in
/// PlayerPrefs, shared by every slot and never rolled back.)
/// </summary>
public class SaveableUIImage : MonoBehaviour
{
    [System.Serializable]
    public class MarkerData
    {
        public GameObject marker;
        [Tooltip("Unique ID for this marker - MUST be unique and never change!")]
        public string uniqueID;
        [Tooltip("Optional: once this other marker is on the map, this one goes away (a rough circle replaced by the real drawing).")]
        public GameObject hiddenBy;
        [HideInInspector] public bool lastKnownState = false;
    }

    [Header("Markers")]
    [Tooltip("Add all child UI Image GameObjects here (each one starts disabled)")]
    public MarkerData[] markers;

    private bool hasInitialized = false;
    int page = -1;

    /// <summary>Which map page these markers are drawn on: 1 under the chalk map (MapMiddleGroup_2), else 0 (the street map).</summary>
    int Page
    {
        get
        {
            if (page < 0)
            {
                page = 0;
                for (var t = transform; t != null; t = t.parent)
                    if (t.name == "MapMiddleGroup_2") { page = 1; break; }
            }
            return page;
        }
    }

    static string Key(MarkerData m) => "mapmarker:" + m.uniqueID;

    private void Start()
    {
        InitializeMarkers();
    }

    private void InitializeMarkers()
    {
        if (hasInitialized)
            return;

        if (markers == null || markers.Length == 0)
        {
            Debug.LogError($"[SaveableUIImage] No markers assigned on {gameObject.name}!");
            return;
        }

        hasInitialized = true;

        for (int i = 0; i < markers.Length; i++)
        {
            if (string.IsNullOrEmpty(markers[i].uniqueID))
                Debug.LogError($"[SaveableUIImage] Marker {i} on {gameObject.name} is missing a uniqueID! Please set it in the inspector.");
        }

        // Show exactly the markers the loaded progress has (and hide any the scene had switched on)
        foreach (var markerData in markers)
        {
            if (markerData.marker == null)
                continue;

            bool wasRevealed = SaveManager.instance != null && !string.IsNullOrEmpty(markerData.uniqueID)
                               && SaveManager.instance.IsInteractableUsed(Key(markerData));
            markerData.marker.SetActive(wasRevealed);
            markerData.lastKnownState = wasRevealed;
        }
        foreach (var markerData in markers) ApplyHiddenBy(markerData);
    }

    static void ApplyHiddenBy(MarkerData m)
    {
        if (m.marker == null || m.hiddenBy == null || !m.hiddenBy.activeSelf || !m.marker.activeSelf) return;
        m.marker.SetActive(false);
        m.lastKnownState = false;   // it stays recorded as revealed; hiddenBy keeps it off
    }

    private void Update() => Poll();

    /// <summary>Called every frame by MapHandler too: this component lives on the map screen, which is switched off most of the
    /// time, and markers are revealed while it's closed. Without this they were only noticed (and saved) once the map was opened.</summary>
    public void Poll()
    {
        if (!hasInitialized) InitializeMarkers();
        if (markers == null || !hasInitialized)
            return;

        // A marker switched on by the game (an OnPlayerTrigger at the place) is recorded in the progress
        foreach (var markerData in markers)
        {
            if (markerData.marker == null)
                continue;

            ApplyHiddenBy(markerData);
            // nothing can be drawn on a map the player doesn't have: a door tried (or a place found) before the map is NOT marked,
            // not even after the map is picked up - trying that door again with the map marks it
            if (markerData.marker.activeSelf && !markerData.lastKnownState && MapHandler.instance != null && !MapHandler.instance.OwnsPage(Page))
            {
                markerData.marker.SetActive(false);
                continue;
            }
            if (markerData.marker.activeSelf != markerData.lastKnownState)
            {
                markerData.lastKnownState = markerData.marker.activeSelf;

                if (markerData.marker.activeSelf && SaveManager.instance != null && !string.IsNullOrEmpty(markerData.uniqueID))
                    SaveManager.instance.RegisterUsedInteractable(Key(markerData));
            }
        }
    }
}
