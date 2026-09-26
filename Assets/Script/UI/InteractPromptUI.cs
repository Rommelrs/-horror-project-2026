using System.Collections;
using UnityEngine;

// Small "Press [icon] to interact" hint. Fades in while the player is near a prompt trigger and
// fades out when they leave; a trigger can also request a delayed fade-out (e.g. right after the
// item is picked up) so pickup feedback has a moment to land before the hint disappears.
public class InteractPromptUI : MonoBehaviour
{
    public static InteractPromptUI instance;

    [Tooltip("How long the fade in/out transition takes.")]
    [SerializeField] float fadeDuration = 0.3f;

    CanvasGroup promptCanvasGroup;
    Coroutine activeRoutine;

    private void Awake()
    {
        instance = this;
        promptCanvasGroup = GetComponent<CanvasGroup>();
        promptCanvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void ShowPrompt()
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        gameObject.SetActive(true);
        activeRoutine = StartCoroutine(Co_ShowPrompt());
    }

    public void HidePrompt(float delay = 0f)
    {
        if (!gameObject.activeSelf) return;

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(Co_HidePrompt(delay));
    }

    IEnumerator Co_ShowPrompt()
    {
        yield return Co_Fade(promptCanvasGroup.alpha, 1f);
        activeRoutine = null;
    }

    IEnumerator Co_HidePrompt(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        yield return Co_Fade(promptCanvasGroup.alpha, 0f);
        gameObject.SetActive(false);
        activeRoutine = null;
    }

    IEnumerator Co_Fade(float from, float to)
    {
        // Unscaled time - opening the inventory (or any other pause) sets Time.timeScale to 0,
        // which would otherwise freeze this fade mid-animation for as long as the game is paused.
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            promptCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }
        promptCanvasGroup.alpha = to;
    }
}
