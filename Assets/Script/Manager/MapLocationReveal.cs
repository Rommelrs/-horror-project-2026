using System.Collections;
using UnityEngine;
using DG.Tweening;

public class MapLocationReveal : MonoBehaviour
{
    [Header("Map References")]
    [Tooltip("The map RectTransform to zoom into (e.g. MapMiddleGroup_2).")]
    [SerializeField] RectTransform mapToZoom;
    [Tooltip("The marker UI GameObject placed manually on the map. Starts inactive.")]
    [SerializeField] CanvasGroup markerCanvasGroup;
    [Tooltip("Which map page to switch to (0 = Map 1, 1 = Map 2).")]
    [SerializeField] int mapPageIndex = 1;

    [Header("Timing")]
    [SerializeField] float fadeInDuration = 0.5f;
    [SerializeField] float zoomDuration = 1.2f;
    [SerializeField] float zoomScale = 2.5f;
    [SerializeField] float markerRevealDelay = 0.4f;
    [SerializeField] float markerFadeDuration = 0.8f;
    [SerializeField] float zoomOutDuration = 1.2f;

    [Header("Audio")]
    [SerializeField] AudioClip revealSound;
    [SerializeField] AudioSource audioSource;

    [Header("Note Trigger")]
    [Tooltip("The exact item name of the note that triggers this reveal. Leave empty to trigger manually.")]
    [SerializeField] string triggerNoteItemName;

    bool revealed = false;
    bool isRunning = false;

    // Static flag checked by MapHandler and PauseHandler to block input during sequence
    public static bool IsSequenceActive = false;

    private void Start()
    {
        if (!string.IsNullOrEmpty(triggerNoteItemName) && ItemInspectionHandler.instance != null)
            ItemInspectionHandler.instance.onCloseInspection += OnInspectionClosed;
    }

    private void OnDestroy()
    {
        if (ItemInspectionHandler.instance != null)
            ItemInspectionHandler.instance.onCloseInspection -= OnInspectionClosed;
    }

    void OnInspectionClosed(string itemName)
    {
        if (revealed) return;
        if (itemName == triggerNoteItemName)
            TriggerReveal();
    }

    // Can also be called manually from a UnityEvent
    public void TriggerReveal()
    {
        if (isRunning) return;
        StartCoroutine(Co_RevealSequence());
    }

    IEnumerator Co_RevealSequence()
    {
        isRunning = true;
        IsSequenceActive = true;

        // Lock player movement and all controls
        Player.instance.pauseMovement = true;

        // Fade to black
        FadeScreenUI.instance.FadeOut();
        yield return new WaitForSecondsRealtime(fadeInDuration);

        // Open map instantly (no coroutine opening)
        MapHandler.instance.EnableMapMenu(true);
        MapHandler.instance.GoToPage(mapPageIndex);

        // Brief pause while black
        yield return new WaitForSecondsRealtime(0.3f);

        // Fade back in to show map
        FadeScreenUI.instance.FadeIn();
        yield return new WaitForSecondsRealtime(fadeInDuration);

        // Zoom into marker position
        if (markerCanvasGroup != null)
        {
            RectTransform markerRect = markerCanvasGroup.GetComponent<RectTransform>();
            Vector2 zoomTarget = Vector2.zero;

            if (markerRect != null)
            {
                // Pan so marker appears at center of screen
                zoomTarget = -markerRect.anchoredPosition * zoomScale;
            }

            mapToZoom.DOScale(zoomScale, zoomDuration).SetUpdate(true).SetEase(Ease.OutCubic);
            mapToZoom.DOAnchorPos(zoomTarget, zoomDuration).SetUpdate(true).SetEase(Ease.OutCubic);
        }

        yield return new WaitForSecondsRealtime(zoomDuration + markerRevealDelay);

        // Reveal marker with fade
        if (markerCanvasGroup != null && !revealed)
        {
            revealed = true;
            markerCanvasGroup.alpha = 0f;
            markerCanvasGroup.gameObject.SetActive(true);

            if (revealSound != null && audioSource != null)
                audioSource.PlayOneShot(revealSound);

            markerCanvasGroup.DOFade(1f, markerFadeDuration).SetUpdate(true);
            yield return new WaitForSecondsRealtime(markerFadeDuration);
        }

        // Zoom back out to full map
        ResetZoom();
        yield return new WaitForSecondsRealtime(0.4f);

        isRunning = false;
        IsSequenceActive = false;

        // Restore player movement
        Player.instance.pauseMovement = false;
    }

    // Call this when the map closes to reset zoom
    public void ResetZoom()
    {
        if (mapToZoom == null) return;
        mapToZoom.DOScale(1f, zoomOutDuration).SetUpdate(true);
        mapToZoom.DOAnchorPos(Vector2.zero, zoomOutDuration).SetUpdate(true);
    }

    private void OnDisable()
    {
        // Reset zoom when map is closed
        if (mapToZoom != null)
        {
            mapToZoom.localScale = Vector3.one;
            mapToZoom.anchoredPosition = Vector2.zero;
        }
    }
}
