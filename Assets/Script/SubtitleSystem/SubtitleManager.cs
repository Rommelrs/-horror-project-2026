using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class SubtitleManager : MonoBehaviour
{
    public static SubtitleManager instance;

    [SerializeField] Text subtitleTxt;
    [SerializeField] bool useTypewriterEffect = true;
    [SerializeField] float letterDelay = 0.03f; // typing speed
    [Tooltip("Unused: subtitles no longer close by themselves (only spoken cutscene lines do, with their voice). Kept so the scenes' saved value stays.")]
    [SerializeField] float autoDismissDelay = 3f;
    //[SerializeField] float groupDelay = 0.4f;
    [SerializeField] InputActionReference continueInput;

    Coroutine showSubtitleCR;

    bool subtitleBusy = false;
    List<string> currentSubtitleList = new List<string>();
    float lastClosedTime;
    public bool IsSubtitleBusy()
    {
        return subtitleBusy;
    }
    public bool IsWithinCooldownPeriod()
    {
        if (Time.time < (lastClosedTime + 0.2f))
            return true;
        else
            return false;
    }

    bool shouldFreezeGame = true;
    // time was frozen by a subtitle and must be given back when the subtitles close - even if a later, non-freezing line
    // replaced the freezing one (the Store door: its own lines freeze, the "locked" line that replaces them doesn't, and the
    // game stayed at timeScale 0 until Esc)
    bool frozeTime = false;

    void Freeze()
    {
        Time.timeScale = 0f;
        frozeTime = true;
    }

    void Unfreeze()
    {
        if (frozeTime && !GameManager.IsPaused) Time.timeScale = 1f;
        frozeTime = false;
    }
    bool waitForContinue = false;   // the last line stays until the player presses interact (no auto-dismiss)
    Action callbackOnCompleted;

    private void Awake()
    {
        instance = this;
    }

    private void OnEnable()
    {
        continueInput.action.Enable();
    }

    private void OnDisable()
    {
        continueInput.action.Disable();
    }

    private void Start()
    {
        //Subscribe to interaction input
        continueInput.action.performed += OnContinueButtonPressed;
    }

    private void OnDestroy()
    {
        continueInput.action.performed -= OnContinueButtonPressed;
    }

    /// <summary>Force close the current subtitle immediately.</summary>
    public void ForceClose()
    {
        if (!subtitleBusy) return;
        StopAllCoroutines();
        subtitleTxt.text = string.Empty;
        Unfreeze();
        subtitleBusy = false;
        lastClosedTime = Time.time;
        callbackOnCompleted?.Invoke();
        callbackOnCompleted = null;
        currentSubtitleList.Clear();
    }

    public bool IsInInteractionCooldownPeriod()
    {
        if (Player.instance == null)
            return false;

        float lastInteractionTime = 0f;
        lastInteractionTime = Player.instance.GetLastInteractionTime();

        if (Time.unscaledTime < lastInteractionTime + 0.2f)
            return true;
        else
            return false;
    }

    public void OnContinueButtonPressed(InputAction.CallbackContext callbackContext)
    {
        if (callbackContext.action.WasPerformedThisFrame())
        {
            if (IsInInteractionCooldownPeriod())
                return;

            if (SubtitleManager.instance.IsSubtitleBusy())
            {
                if (currentSubtitleList == null || currentSubtitleList.Count <= 0)
                {
                    //No more subtitle to play so exit out of subtitle mode
                    subtitleTxt.text = string.Empty;

                    Unfreeze();

                    subtitleBusy = false;
                    lastClosedTime = Time.time;

                    //Trigger Callback Event
                    if (callbackOnCompleted != null)
                        callbackOnCompleted?.Invoke();

                    callbackOnCompleted = null;

                    this.StopAllCoroutines();
                }
                else
                {
                    //Continue to next group subtitle
                    UpdateGroupSubtitle(currentSubtitleList.ToArray());
                }
            }
        }
    }

    /// <param name="showFor">Spoken lines (cutscenes): above 0, the line appears whole at once (no typewriter) and closes
    /// after this many seconds, so it keeps pace with the voice.</param>
    /// <param name="waitForContinue">The line stays until the player presses interact instead of closing by itself.</param>
    public static void ShowSubtitle(LocalizedString localizedString, bool freezeGame = true, Action callbakActionOnCompleted = null, float showFor = -1f, bool waitForContinue = false)
    {
        if (instance != null)
        {
            string subtitle = localizedString.GetLocalizedString();
            instance.shouldFreezeGame = freezeGame;
            instance.waitForContinue = waitForContinue;
            instance.callbackOnCompleted = callbakActionOnCompleted;
            instance.currentSubtitleList = new List<string>();
            instance.UpdateSubtitle(subtitle, showFor);
        }
    }

    public static void ShowSubtitleGroup(LocalizedString[] localizedStringList, bool freezeGame = true, Action callbakActionOnCompleted = null, bool waitForContinue = false)
    {
        if (instance != null)
        {
            List<string> stringList = new List<string>();
            if(localizedStringList != null && localizedStringList.Length > 0)
            {
                for (int i = 0; i < localizedStringList.Length; i++)
                {
                    stringList.Add(localizedStringList[i].GetLocalizedString());
                }
            }

            instance.shouldFreezeGame = freezeGame;
            instance.waitForContinue = waitForContinue;
            instance.callbackOnCompleted = callbakActionOnCompleted;
            instance.UpdateGroupSubtitle(stringList.ToArray());
        }
    }

    void UpdateGroupSubtitle(string[] subtitleList)
    {
        List<string> newSubtitleGroup = new List<string>();
        string subtitle = string.Empty;

        for (int i = 0; i < subtitleList.Length; i++)
        {
            if (i == 0)
                subtitle = subtitleList[i];
            else
                newSubtitleGroup.Add(subtitleList[i]);
        }

        currentSubtitleList = newSubtitleGroup;
        UpdateSubtitle(subtitle);
    }

    void UpdateSubtitle(string subtitle, float showFor = -1f)
    {
        if (showSubtitleCR != null) StopCoroutine(showSubtitleCR);
        showSubtitleCR = StartCoroutine(Co_UpdateSubtitle(subtitle, showFor));
    }

    IEnumerator Co_UpdateSubtitle(string subtitle, float showFor = -1f)
    {
        bool spoken = showFor > 0f;
        subtitleBusy = true;
        if (shouldFreezeGame) Freeze();
        else Unfreeze();   // a line that doesn't freeze replaced one that did: the game runs again

        subtitleTxt.text = string.Empty;

        if (useTypewriterEffect && !spoken)
        {
            for (int i = 0; i < subtitle.Length; i++)
            {
                subtitleTxt.text += subtitle[i];
                yield return new WaitForSecondsRealtime(letterDelay);
            }
        }
        else
        {
            subtitleTxt.text = subtitle;
        }

        // The last line of the group: a spoken cutscene line closes with its voice; every other line stays until the
        // player presses interact (OnContinueButtonPressed) - they read at their own pace, nothing is skipped for them.
        if (currentSubtitleList == null || currentSubtitleList.Count <= 0)
        {
            if (!spoken) yield break;
            yield return new WaitForSecondsRealtime(showFor);
            
            // Close subtitle
            subtitleTxt.text = string.Empty;

            Unfreeze();

            subtitleBusy = false;
            lastClosedTime = Time.time;

            //Trigger Callback Event
            if (callbackOnCompleted != null)
                callbackOnCompleted?.Invoke();

            callbackOnCompleted = null;
        }
    }
}
