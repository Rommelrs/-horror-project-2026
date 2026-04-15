using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class AudioInteractionHandler : MonoBehaviour
{
    public static AudioInteractionHandler instance;

    [SerializeField] InputActionReference interactionInput;
    [SerializeField] GameObject interactionPanel;
    [SerializeField] CanvasGroup interactionCanvasGroup;

    AudioInteraction currentAudioInteraction;
    DG.Tweening.Sequence sequence;
    AudioSource audioSource;

    private void Awake()
    {
        instance = this;
        audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        interactionInput.action.Enable();
    }

    private void OnDisable()
    {
        interactionInput.action.Disable();
    }

    private void Start()
    {
        //Subscribe to interaction input
        interactionInput.action.performed += OnInteractButtonPressed;
    }

    public void OnInteractButtonPressed(InputAction.CallbackContext callbackContext)
    {
        if (callbackContext.performed)
        {
            if (currentAudioInteraction != null)
            {
                //Interacted
                audioSource.PlayOneShot(currentAudioInteraction.audioClip);

                //Destroy current audio interaction if enabled
                if (currentAudioInteraction.destroyAfterInteraction)
                {
                    Destroy(currentAudioInteraction.gameObject);
                    currentAudioInteraction = null;
                    EndAudioInteraction();
                }
            }
        }
    }

    public void AudioInteractionTriggerEnter(AudioInteraction audioInteraction)
    {
        //Update Current Audio Interaction
        currentAudioInteraction = audioInteraction;

        ////Enable Interaction Panel
        //interactionPanel.gameObject.SetActive(true);
        //interactionPanel.transform.position = audioInteraction.interactPoint.position;
        //interactionCanvasGroup.alpha = 0f;

        ////Animate interaction canvas group
        //sequence = DOTween.Sequence();
        //sequence.Append(interactionCanvasGroup.DOFade(1f, 0.5f).SetEase(Ease.Linear));
        //sequence.ForceInit();
    }

    void EndAudioInteraction()
    {
        //Reset Current Audio Interaction
        currentAudioInteraction = null;

        //Kill animation
        //if (sequence != null)
        //    sequence.Kill();

        //Disable Interaction Panel
        interactionCanvasGroup.alpha = 0f;
        interactionPanel.gameObject.SetActive(false);
    }

    public void AudioInteractionTriggerExit(AudioInteraction audioInteraction)
    {
        EndAudioInteraction();
    }
}
