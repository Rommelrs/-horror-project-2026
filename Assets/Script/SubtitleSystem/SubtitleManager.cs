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
    [SerializeField] float autoDismissDelay = 3f; // auto-dismiss last subtitle after this delay
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

                    if (shouldFreezeGame && !GameManager.IsPaused)
                        Time.timeScale = 1f;

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

    public static void ShowSubtitle(LocalizedString localizedString, bool freezeGame = true, Action callbakActionOnCompleted = null)
    {
        if (instance != null)
        {
            string subtitle = localizedString.GetLocalizedString();
            instance.shouldFreezeGame = freezeGame;
            instance.callbackOnCompleted = callbakActionOnCompleted;
            instance.currentSubtitleList = new List<string>();
            instance.UpdateSubtitle(subtitle);
        }
    }

    public static void ShowSubtitleGroup(LocalizedString[] localizedStringList, bool freezeGame = true, Action callbakActionOnCompleted = null)
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

    void UpdateSubtitle(string subtitle)
    {
        if (showSubtitleCR != null) StopCoroutine(showSubtitleCR);
        showSubtitleCR = StartCoroutine(Co_UpdateSubtitle(subtitle));
    }

    IEnumerator Co_UpdateSubtitle(string subtitle)
    {
        subtitleBusy = true;
        if (shouldFreezeGame)
            Time.timeScale = 0f;

        subtitleTxt.text = string.Empty;

        if (useTypewriterEffect)
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

        // Auto-dismiss last subtitle if no more in queue
        if (currentSubtitleList == null || currentSubtitleList.Count <= 0)
        {
            yield return new WaitForSecondsRealtime(autoDismissDelay);
            
            // Close subtitle
            subtitleTxt.text = string.Empty;

            if (shouldFreezeGame && !GameManager.IsPaused)
                Time.timeScale = 1f;

            subtitleBusy = false;
            lastClosedTime = Time.time;

            //Trigger Callback Event
            if (callbackOnCompleted != null)
                callbackOnCompleted?.Invoke();

            callbackOnCompleted = null;
        }
    }
}
