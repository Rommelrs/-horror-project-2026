using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Takes items out of the player's inventory when this cutscene's timeline finishes (played through or skipped): for an
/// item whose job ends with the story beat, e.g. the Alley 2 Gate Key after the Questioner horde cutscene.
/// </summary>
[RequireComponent(typeof(PlayableDirector))]
public class RemoveItemsAfterCutscene : MonoBehaviour
{
    [SerializeField] Item[] itemsToRemove;

    PlayableDirector director;
    bool started;

    void Awake()
    {
        director = GetComponent<PlayableDirector>();
        director.played += OnPlayed;
        director.stopped += OnStopped;
    }

    void OnDestroy()
    {
        if (director == null) return;
        director.played -= OnPlayed;
        director.stopped -= OnStopped;
    }

    // a play-on-awake director can start before Awake's subscription: a running director counts as played
    void Start() { if (director.state == PlayState.Playing) started = true; }

    void OnPlayed(PlayableDirector d) => started = true;

    void OnStopped(PlayableDirector d)
    {
        if (!started) return;     // only once it has really run
        started = false;
        if (Player.instance == null || Player.instance.inventory == null || itemsToRemove == null) return;
        foreach (var item in itemsToRemove)
            if (item != null)
                while (Player.instance.inventory.HasItem(item)) if (!Player.instance.inventory.RemoveItem(item)) break;
    }
}
