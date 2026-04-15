using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class SubtitleTrigger : MonoBehaviour
{
    [SerializeField] bool autoTriggerOnStart = false;
    [SerializeField] float autoTriggerOnStartDelay = 0f;

    [SerializeField] float lifeTime = 3f;
    [SerializeField] bool triggerOnce = false;
    [SerializeField] bool freezeGame = true;

    [System.Serializable]
    public enum SubtitleTriggerType
    {
        Default,
        OnTriggerEnter
    }
    [SerializeField] SubtitleTriggerType subtitleTriggerType;

    [System.Serializable]
    public enum SubtitleType
    {
        Single,
        Group
    }
    [SerializeField] SubtitleType subtitleType;

    [System.Serializable]
    public enum CompletedEventType
    {
        None,
        CloseInspectionMenu,
        CloseCashRegisterMenu,
        TriggerCompletedEvent
    }
    [SerializeField] CompletedEventType completedEventType;
    [SerializeField] UnityEvent onSubtitleTriggerCompleted;

    //[Header("OLD Subtitle")]
    //[SerializeField] [TextArea] string subtitleText;
    //[SerializeField] [TextArea] string[] subtitleGroupText;

    [Header("Subtitle Ref")]
    [SerializeField] LocalizedString subtitleRef;
    [SerializeField] LocalizedString[] subtitleGroupRef;

    [Header("Audio")]
    [SerializeField] AudioClip audioClip;
    [SerializeField] AudioSource audioSource;

    bool alreadyTriggered = false;

    private IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();    

        while (LoadingHandler.instance && LoadingHandler.IsLoading())
            yield return new WaitForEndOfFrame();

        yield return new WaitForEndOfFrame();

        if(autoTriggerOnStartDelay > 0)
            yield return new WaitForSecondsRealtime(autoTriggerOnStartDelay);

        if (autoTriggerOnStart)
            TriggerSubtitle();
    }

    private void OnDestroy()
    {
        if (onSubtitleTriggerCompleted != null)
            onSubtitleTriggerCompleted.RemoveAllListeners();
    }

    public void TriggerSubtitle()
    {
        if (triggerOnce && alreadyTriggered)
            return;

        alreadyTriggered = true;
        
        // Mark as triggered in save system
        SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
        if (saveableTrigger != null)
        {
            saveableTrigger.MarkAsTriggered();
        }

        PlayAudio();

        if (completedEventType == CompletedEventType.CloseInspectionMenu)
        {
            if (subtitleType == SubtitleType.Single)
                SubtitleManager.ShowSubtitle(subtitleRef, freezeGame, CloseInspectionMenu);
            else if (subtitleType == SubtitleType.Group)
                SubtitleManager.ShowSubtitleGroup(subtitleGroupRef, freezeGame, CloseInspectionMenu);
        }
        else if(completedEventType == CompletedEventType.CloseCashRegisterMenu)
        {
            if (subtitleType == SubtitleType.Single)
                SubtitleManager.ShowSubtitle(subtitleRef, freezeGame, CloseCashRegisterMenu);
            else if (subtitleType == SubtitleType.Group)
                SubtitleManager.ShowSubtitleGroup(subtitleGroupRef, freezeGame, CloseCashRegisterMenu);
        }
        else if(completedEventType == CompletedEventType.TriggerCompletedEvent)
        {
            if (subtitleType == SubtitleType.Single)
                SubtitleManager.ShowSubtitle(subtitleRef, freezeGame, TriggerCompletedEvent);
            else if (subtitleType == SubtitleType.Group)
                SubtitleManager.ShowSubtitleGroup(subtitleGroupRef, freezeGame, TriggerCompletedEvent);
        }
        else
        {
            if (subtitleType == SubtitleType.Single)
                SubtitleManager.ShowSubtitle(subtitleRef, freezeGame);
            else if (subtitleType == SubtitleType.Group)
                SubtitleManager.ShowSubtitleGroup(subtitleGroupRef, freezeGame);
        }
    }

    public void TriggerSubtitleAfterDelay(float delay)
    {
        StartCoroutine(Co_TriggerSubtitleAfterDelay(delay));
    }

    IEnumerator Co_TriggerSubtitleAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        TriggerSubtitle();
    }

    private void OnTriggerEnter(Collider other)
    {
        if(subtitleTriggerType == SubtitleTriggerType.OnTriggerEnter && other.CompareTag("Player"))
        {
            // Check save system
            SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
            if (saveableTrigger != null && saveableTrigger.WasAlreadyTriggered())
            {
                alreadyTriggered = true;
                return;
            }
            
            TriggerSubtitle();
        }
    }

    void CloseInspectionMenu()
    {
        //If Item Inspection Menu is active then Close it
        if (ItemInspectionHandler.instance && ItemInspectionHandler.instance.InspectionMenuIsActive())
        {
            ItemInspectionHandler.instance.CloseInspectionMenu();
        }
    }

    void CloseCashRegisterMenu()
    {
        //If CashRegister Menu is active then Close it
        if (CashRegisterInteractable.instance && CashRegisterInteractable.instance.CashRegisterMenuIsActive())
        {
            CashRegisterInteractable.instance.CloseCashRegisterMenu();
        }
    }

    void TriggerCompletedEvent()
    {
        if (onSubtitleTriggerCompleted != null)
            onSubtitleTriggerCompleted?.Invoke();
    }

    void PlayAudio()
    {
        if (audioClip == null)
            return;

        if (audioSource != null)
            audioSource.PlayOneShot(audioClip);
        else
            AudioSource.PlayClipAtPoint(audioClip, transform.position);
    }
}
