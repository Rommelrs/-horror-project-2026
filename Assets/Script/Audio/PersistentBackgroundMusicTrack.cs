using UnityEngine;

// Attach to each background-music GameObject (e.g. BackgroundMusic, BackgroundMusic Alley 3).
// Whenever a track turns on, it turns off the others and remembers itself as the
// current track via SaveManager, so BackgroundMusicRestorer can bring the right one
// back after a scene reload (death -> Continue, or loading a save) - the story events
// that normally turn these on (cutscenes, burn events, door interactions) only fire
// once and won't replay after the player has already passed them.
public class PersistentBackgroundMusicTrack : MonoBehaviour
{
    [Tooltip("Unique id for this track, e.g. \"Default\" or \"Alley3\".")]
    [SerializeField] string trackId;
    [SerializeField] PersistentBackgroundMusicTrack[] otherTracks;

    public string TrackId => trackId;

    private void OnEnable()
    {
        foreach (var other in otherTracks)
        {
            if (other != null && other.gameObject.activeSelf)
                other.gameObject.SetActive(false);
        }

        if (SaveManager.instance != null)
            SaveManager.instance.RegisterCurrentMusicTrack(trackId);
    }
}
