using System.Collections;
using UnityEngine;

// One-off "Press [M] to open Map" hint. Hook ShowHint() to an event that auto-opens the map for the player
// (e.g. the candy wrapper's onImageClosed). It waits until the map reveal sequence has finished and the
// map is closed again, then shows the prompt until the player opens the map themselves (or it times out).
// The prompt is an InteractPromptUI copy with "Register As Instance" turned off so it doesn't replace the
// normal interact prompt.
public class MapHintPrompt : MonoBehaviour
{
    [SerializeField] InteractPromptUI prompt;

    [Tooltip("Pause between the map closing and the hint appearing.")]
    [SerializeField] float delayAfterMapClosed = 1f;

    [Tooltip("The hint disappears on its own after being on screen this long (it only counts while it is actually visible).")]
    [SerializeField] float visibleDuration = 15f;

    Coroutine routine;
    bool finished;

    public void ShowHint()
    {
        if (finished || routine != null)
            return;

        routine = StartCoroutine(Co_Hint());
    }

    IEnumerator Co_Hint()
    {
        // Let the event that called us (and the reveal sequence it starts) get going first
        yield return null;

        // The reveal sequence opens the map by itself - wait for it to end and for the player to close it
        while (MapLocationReveal.IsSequenceActive || MapIsOpen())
            yield return null;

        yield return new WaitForSecondsRealtime(delayAfterMapClosed);

        bool visible = false;
        float shownTime = 0f;

        while (shownTime < visibleDuration)
        {
            // The player already knows how to open it
            if (MapIsOpen())
                break;

            bool shouldShow = CanShow();
            if (shouldShow != visible)
            {
                visible = shouldShow;
                if (visible) prompt.ShowPrompt(); else prompt.HidePrompt();
            }

            if (visible)
                shownTime += Time.unscaledDeltaTime;

            yield return null;
        }

        prompt.HidePrompt();
        finished = true;
        routine = null;
    }

    static bool MapIsOpen()
    {
        return MapHandler.instance != null && MapHandler.instance.MapIsActive();
    }

    static bool CanShow()
    {
        if (Player.instance == null || Player.instance.IsDead()) return false;
        if (MapHandler.instance == null || (!MapHandler.instance.hasMap1 && !MapHandler.instance.hasMap2)) return false;
        if (GameManager.IsPaused) return false;
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) return false;
        if (InventoryUI.instance != null && InventoryUI.instance.InventoryIsActive()) return false;
        if (ItemInspectionHandler.instance != null && ItemInspectionHandler.instance.InspectionMenuIsActive()) return false;
        return true;
    }
}
