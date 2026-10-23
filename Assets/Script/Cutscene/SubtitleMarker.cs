using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Timeline marker that shows one subtitle line (from the Subtitles localization table) for a fixed time.
// Put it on the timeline's marker row; a CutsceneSubtitleReceiver on the PlayableDirector's GameObject shows it.
// The line does not freeze the game and closes by itself after `duration` (or when the next marker fires).
[System.ComponentModel.DisplayName("Subtitle")]
public class SubtitleMarker : Marker, INotification
{
    public LocalizedString line;
    [Tooltip("Seconds the line stays on screen. Usually the length of its voice clip.")]
    public float duration = 2.5f;
    [Tooltip("Speaker id for the voice placeholder (only used to keep the timeline readable).")]
    public string lineId;

    public PropertyName id => new PropertyName("SubtitleMarker");
}
