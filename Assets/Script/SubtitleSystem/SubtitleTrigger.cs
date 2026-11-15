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

    [Tooltip("Spoken lines only (see 'Spoken Line'): how long the line stays, in step with the voice.")]
    [SerializeField] float lifeTime = 3f;
    [Tooltip("A line said out loud in a cutscene: it shows whole at once and closes after 'Life Time' seconds, with the voice. Every other subtitle stays until the player presses interact.")]
    [SerializeField] bool spokenLine = false;
    [SerializeField] bool triggerOnce = false;
    [SerializeField] bool freezeGame = true;
    [Tooltip("Unused: every subtitle that is not a spoken line now waits for the player to press interact.")]
    [SerializeField] bool waitForInteract = false;

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

    /// <summary>The lines this trigger shows (a story beat can swap them, e.g. the Store's entrance door before / after the Runner).</summary>
    public LocalizedString[] SubtitleGroup
    {
        get => subtitleGroupRef;
        set => subtitleGroupRef = value;
    }

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

        Action onDone = completedEventType switch
        {
            CompletedEventType.CloseInspectionMenu => CloseInspectionMenu,
            CompletedEventType.CloseCashRegisterMenu => CloseCashRegisterMenu,
            CompletedEventType.TriggerCompletedEvent => TriggerCompletedEvent,
            _ => null
        };
        if (subtitleType == SubtitleType.Single)
            SubtitleManager.ShowSubtitle(subtitleRef, freezeGame, onDone, spokenLine ? Mathf.Max(0.5f, lifeTime) : -1f, waitForInteract);
        else if (subtitleType == SubtitleType.Group)
            SubtitleManager.ShowSubtitleGroup(subtitleGroupRef, freezeGame, onDone, waitForInteract);
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
        {
            audioSource.PlayOneShot(audioClip);
        }
        else if (SoundEffectManager.instance != null)
        {
            // AudioSource.PlayClipAtPoint can't route through the mixer, so it ignores the SFX
            // volume slider - go through SoundEffectManager (routed to the SFX group) instead.
            SoundEffectManager.instance.PlaySFXAtPosition(audioClip, transform.position);
        }
        else
        {
            AudioSource.PlayClipAtPoint(audioClip, transform.position);
        }
    }
}
