using UnityEngine;

// Place on an always-active GameObject (e.g. the Managers group), never on the music
// tracks themselves - they start inactive by default, and Start() never runs on an
// inactive GameObject, so the restore logic can't live there.
//
// On scene start, re-applies whichever background music track was last registered
// with SaveManager, since the one-time story events that normally turn these tracks
// on/off (cutscenes, burn events, door interactions) won't fire again once the player
// has already passed them - which is why the music used to break after dying and
// pressing Continue, or after loading a save.
public class BackgroundMusicRestorer : MonoBehaviour
{
    [SerializeField] PersistentBackgroundMusicTrack[] tracks;

    private void Start()
    {
        if (SaveManager.instance == null)
            return;

        string savedTrack = SaveManager.instance.GetCurrentMusicTrack();
        if (string.IsNullOrEmpty(savedTrack))
            return;

        foreach (var track in tracks)
        {
            if (track == null)
                continue;

            bool shouldBeActive = track.TrackId == savedTrack;
            if (track.gameObject.activeSelf != shouldBeActive)
                track.gameObject.SetActive(shouldBeActive);
        }
    }
}
