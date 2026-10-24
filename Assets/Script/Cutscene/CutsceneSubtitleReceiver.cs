using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

// Receives SubtitleMarker notifications from the PlayableDirector on this GameObject and shows them through
// SubtitleManager without freezing the game, closing each line after its duration.
public class CutsceneSubtitleReceiver : MonoBehaviour, INotificationReceiver
{
    Coroutine closeCR;
    int shown;

    public void OnNotify(Playable origin, INotification notification, object context)
    {
        if (notification is not SubtitleMarker marker || marker.line == null || marker.line.IsEmpty) return;
        Show(marker);
    }

    void Show(SubtitleMarker marker)
    {
        if (SubtitleManager.instance == null) return;
        if (SubtitleManager.instance.IsSubtitleBusy()) SubtitleManager.instance.ForceClose();
        SubtitleManager.ShowSubtitle(marker.line, false, null, marker.duration);   // whole at once, paced by the voice
        shown++;
        if (closeCR != null) StopCoroutine(closeCR);
        closeCR = StartCoroutine(Co_Close(shown, marker.duration));
    }

    IEnumerator Co_Close(int which, float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0.5f, duration));
        if (which == shown) CloseNow();
    }

    /// <summary>Close whatever line is up (cutscene skipped or finished).</summary>
    public void CloseNow()
    {
        if (closeCR != null) { StopCoroutine(closeCR); closeCR = null; }
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) SubtitleManager.instance.ForceClose();
    }
}
