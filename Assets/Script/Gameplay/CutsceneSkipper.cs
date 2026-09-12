using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

public class CutsceneSkipper : MonoBehaviour
{
    [Header("Timeline")]
    [Tooltip("Add all skippable cutscene directors in this scene.")]
    [SerializeField] PlayableDirector[] directors;

    [Header("Skip Settings")]
    [SerializeField] KeyCode skipKey = KeyCode.E;
    [SerializeField] float holdDuration = 2f;
    [SerializeField] float hintHideDelay = 2f;
    [SerializeField] float hintFadeDuration = 0.4f;
    [SerializeField] float skipFadeDuration = 0.5f;
    [Tooltip("If this cutscene transitions to another scene, enter the scene name here. Leave empty if no scene load needed.")]
    [SerializeField] string sceneToLoadOnSkip;

    [Header("UI References")]
    [SerializeField] CanvasGroup skipPanel;       // The whole skip UI panel
    [SerializeField] Image fillRing;              // Image with Fill Method = Radial 360
    [SerializeField] TMP_Text skipHintText;       // e.g. "Hold E to skip"

    float holdProgress = 0f;
    bool isSkipping = false;
    Coroutine hidePanelCR;

    void Awake()
    {
        if (skipPanel != null)
        {
            skipPanel.alpha = 0f;
            skipPanel.gameObject.SetActive(false);
        }

        if (fillRing != null)
            fillRing.fillAmount = 0f;

        if (skipHintText != null)
            skipHintText.text = "Hold " + skipKey.ToString() + " to skip";
    }

    PlayableDirector GetActiveDirector()
    {
        if (directors == null) return null;
        foreach (var d in directors)
            if (d != null && d.state == PlayState.Playing)
                return d;
        return null;
    }

    void Update()
    {
        if (isSkipping) return;

        PlayableDirector active = GetActiveDirector();
        if (active == null) return;

        // Any key press shows the skip hint
        if (Input.anyKeyDown)
            ShowSkipHint();

        // Hold skip key to fill ring
        if (Input.GetKey(skipKey))
        {
            ShowSkipHint();

            holdProgress += Time.unscaledDeltaTime / holdDuration;
            holdProgress = Mathf.Clamp01(holdProgress);

            if (fillRing != null)
                fillRing.fillAmount = holdProgress;

            if (holdProgress >= 1f)
                Skip(active);
        }
        else
        {
            holdProgress -= Time.unscaledDeltaTime / holdDuration;
            holdProgress = Mathf.Clamp01(holdProgress);

            if (fillRing != null)
                fillRing.fillAmount = holdProgress;
        }
    }

    void ShowSkipHint()
    {
        if (skipPanel == null) return;

        if (!skipPanel.gameObject.activeSelf)
        {
            skipPanel.alpha = 0f;
            skipPanel.gameObject.SetActive(true);
            skipPanel.DOFade(1f, hintFadeDuration).SetUpdate(true);
        }

        // Reset hide timer
        if (hidePanelCR != null) StopCoroutine(hidePanelCR);
        hidePanelCR = StartCoroutine(Co_HideAfterDelay());
    }

    IEnumerator Co_HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(hintHideDelay);

        if (!Input.GetKey(skipKey))
        {
            skipPanel.DOFade(0f, hintFadeDuration).SetUpdate(true).OnComplete(() =>
                skipPanel.gameObject.SetActive(false));
        }
    }

    void Skip(PlayableDirector d)
    {
        isSkipping = true;

        if (skipPanel != null)
        {
            skipPanel.DOKill();
            skipPanel.alpha = 0f;
            skipPanel.gameObject.SetActive(false);
        }

        StartCoroutine(Co_SkipWithFade(d));
    }

    IEnumerator Co_SkipWithFade(PlayableDirector d)
    {
        // Fade to black first
        if (FadeScreenUI.instance != null)
            FadeScreenUI.instance.FadeOut();
        yield return new WaitForSecondsRealtime(skipFadeDuration);

        // Stop the timeline while screen is black
        d.time = d.duration;
        d.Evaluate();
        d.Stop();

        // If this cutscene leads to another scene, load it
        if (!string.IsNullOrEmpty(sceneToLoadOnSkip))
        {
            if (LoadingHandler.instance != null)
                LoadingHandler.instance.LoadScene(sceneToLoadOnSkip);
            else
                SceneManager.LoadScene(sceneToLoadOnSkip);
            yield break;
        }

        // Otherwise fade back in to gameplay
        if (FadeScreenUI.instance != null)
            FadeScreenUI.instance.FadeIn();

        // Reset for next cutscene
        isSkipping = false;
        holdProgress = 0f;
        if (fillRing != null) fillRing.fillAmount = 0f;
    }

    // Call this to enable/disable the skipper (e.g. disable during non-skippable cutscenes)
    public void SetSkippable(bool value)
    {
        enabled = value;
        isSkipping = false;
        holdProgress = 0f;
        if (fillRing != null) fillRing.fillAmount = 0f;
        if (skipPanel != null) skipPanel.gameObject.SetActive(false);
    }
}
