using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FadeScreenUI : MonoBehaviour
{
    public static FadeScreenUI instance;

    [SerializeField] private CanvasGroup canvasGroup; // Reference to the CanvasGroup component
    [SerializeField] private float fadeDuration = 1f; // Duration of the fade effect
    [SerializeField] private Image fadeImage; // Optional image to display during fade

    Coroutine fadeCoroutine;
    Coroutine imageFadeCoroutine;

    private void Awake()
    {
        instance = this;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        //Face in on start
        FadeIn();
    }

    //Smoothly Fade in the screen
    public void FadeIn()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(Fade(0f));
    }

    //Smoothly Fade out the screen
    public void FadeOut()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(Fade(1f));
    }

    //Coroutine to handle the fade effect that smoothly transitions the alpha value of the CanvasGroup
    private IEnumerator Fade(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    /// <summary>
    /// Fade to black, show an image, wait, then fade back in
    /// </summary>
    /// <param name="image">The sprite to display</param>
    /// <param name="displayDuration">How long to show the image</param>
    /// <param name="onComplete">Optional callback when complete</param>
    public void FadeToImage(Sprite image, float displayDuration, Action onComplete = null)
    {
        if (imageFadeCoroutine != null) StopCoroutine(imageFadeCoroutine);
        imageFadeCoroutine = StartCoroutine(FadeToImageCoroutine(image, displayDuration, onComplete));
    }

    /// <summary>
    /// Fade to black, show an image, wait, then fade back in (with custom fade duration)
    /// </summary>
    public void FadeToImage(Sprite image, float displayDuration, float customFadeDuration, Action onComplete = null)
    {
        if (imageFadeCoroutine != null) StopCoroutine(imageFadeCoroutine);
        imageFadeCoroutine = StartCoroutine(FadeToImageCoroutine(image, displayDuration, onComplete, customFadeDuration));
    }

    private IEnumerator FadeToImageCoroutine(Sprite image, float displayDuration, Action onComplete, float? customFadeDuration = null)
    {
        float duration = customFadeDuration ?? fadeDuration;

        // Hide the image initially
        if (fadeImage != null)
        {
            fadeImage.sprite = null;
            fadeImage.enabled = false;
        }

        // Fade to black
        yield return StartCoroutine(FadeWithDuration(1f, duration));

        // Show the image
        if (fadeImage != null && image != null)
        {
            fadeImage.sprite = image;
            fadeImage.enabled = true;
        }

        // Wait for display duration
        yield return new WaitForSecondsRealtime(displayDuration);

        // Hide the image before fading back
        if (fadeImage != null)
        {
            fadeImage.sprite = null;
            fadeImage.enabled = false;
        }

        // Fade back in
        yield return StartCoroutine(FadeWithDuration(0f, duration));

        onComplete?.Invoke();
    }

    private IEnumerator FadeWithDuration(float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    /// <summary>
    /// Show an image on the fade screen
    /// </summary>
    public void ShowImage(Sprite image)
    {
        if (fadeImage != null && image != null)
        {
            fadeImage.sprite = image;
            fadeImage.enabled = true;
        }
    }

    /// <summary>
    /// Hide the image on the fade screen
    /// </summary>
    public void HideImage()
    {
        if (fadeImage != null)
        {
            fadeImage.sprite = null;
            fadeImage.enabled = false;
        }
    }
}
