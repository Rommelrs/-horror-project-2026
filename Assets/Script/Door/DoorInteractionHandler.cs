using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DoorInteractionHandler : MonoBehaviour
{
    public static DoorInteractionHandler instance;

    [SerializeField] InputActionReference doorInteractionInput;
    //[SerializeField] GameObject interactionPanel;
    //[SerializeField] CanvasGroup interactionCanvasGroup;
    [SerializeField] AudioClip doorOpenClip;
    [SerializeField] AudioClip doorCloseClip;
    [SerializeField] AudioClip doorLockedClip;

    DoorInteractable currentInteractable;
    DoorTriggerType currentDoorTriggerType;

    //DG.Tweening.Sequence sequence;
    AudioSource audioSource;
    bool busy = false;
    CharacterController characterController;

    private void Awake()
    {
        instance = this;
        audioSource = GetComponent<AudioSource>();
        characterController = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        doorInteractionInput.action.Enable();
    }

    private void OnDisable()
    {
        doorInteractionInput.action.Disable();
    }

    private void Start()
    {
        //Subscribe to door interaction input
        doorInteractionInput.action.performed += OnDoorInteractionButtonPressed;
    }

    public void OnDoorInteractionButtonPressed(InputAction.CallbackContext callbackContext)
    {
        if (SubtitleManager.instance != null && (SubtitleManager.instance.IsSubtitleBusy() || SubtitleManager.instance.IsWithinCooldownPeriod()))
            return;

        if (GameManager.IsPaused)
            return;

        if (ItemInspectionHandler.instance && ItemInspectionHandler.instance.InspectionMenuIsActive())
            return;

        //On Interact Button pressed
        if (callbackContext.performed)
        {
            // Clear stale reference if the door GameObject was disabled (e.g. during a cutscene)
            // Unity does not reliably call OnTriggerExit when a GameObject is disabled
            if (currentInteractable != null && !currentInteractable.gameObject.activeInHierarchy)
                currentInteractable = null;

            if (currentInteractable != null && busy == false)
            {
                //Check if door requires certain key
                if (currentInteractable.requiresKey)
                {
                    //Check if has key
                    bool hasKey = false;
                    List<Inventory.ItemStack> itemStacks = Player.instance.inventory.GetItems();

                    if (itemStacks != null && itemStacks.Count > 0)
                    {
                        for (int i = 0; i < itemStacks.Count; i++)
                        {
                            if (itemStacks[i].item == null)
                                continue;

                            if (itemStacks[i].item.itemType == ItemType.Key && itemStacks[i].item.keyCode == currentInteractable.keyCodeRequired)
                            {
                                hasKey = true;
                                break;
                            }
                        }
                    }

                    //Unsuccessful to find correct key
                    if (!hasKey)
                    {
                        //Interaction Failed
                        if (currentInteractable.OnInteractionFailed != null)
                            currentInteractable.OnInteractionFailed.Invoke();

                        AudioClip lockedClip = currentInteractable.customLockedClip != null ? currentInteractable.customLockedClip : doorLockedClip;
                        audioSource.PlayOneShot(lockedClip);
                        return;
                    }
                }

                //Has Door Lock
                if (currentInteractable.hasDoorLock)
                {
                    //Interaction Failed
                    if (currentInteractable.OnInteractionFailed != null)
                        currentInteractable.OnInteractionFailed.Invoke();

                    //Trigger locked subtitle if player has required item
                    if (currentInteractable.ShouldTriggerLockedSubtitle())
                    {
                        currentInteractable.lockedSubtitleTrigger.TriggerSubtitle();
                    }

                    AudioClip lockedClip = currentInteractable.customLockedClip != null ? currentInteractable.customLockedClip : doorLockedClip;
                    audioSource.PlayOneShot(lockedClip);
                    return;
                }

                //Start New Door Enter Sequence
                StartCoroutine(Co_ActivateDoorEnterSequence(currentInteractable, currentDoorTriggerType));

                //Interacted Event
                if (currentInteractable.OnInteracted != null)
                    currentInteractable.OnInteracted.Invoke();
            }
        }
    }

    IEnumerator Co_ActivateDoorEnterSequence(DoorInteractable doorInteractable, DoorTriggerType doorTriggerType)
    {
        busy = true;

        //Fade screen to black
        FadeScreenUI.instance.FadeOut();

        yield return new WaitForSeconds(0.5f);

        //Play door open SFX
        AudioClip openClip = doorInteractable.customOpenClip != null ? doorInteractable.customOpenClip : doorOpenClip;
        audioSource.PlayOneShot(openClip);

        yield return new WaitForSeconds(0.6f);

        characterController.enabled = false;

        yield return new WaitForEndOfFrame();

        //Teleport Player
        if (doorTriggerType == DoorTriggerType.Enter)
        {
            transform.position = doorInteractable.exitPoint.position;
            transform.rotation = doorInteractable.exitPoint.rotation;
        }
        else if (doorTriggerType == DoorTriggerType.Exit)
        {
            transform.position = doorInteractable.enterPoint.position;
            transform.rotation = doorInteractable.enterPoint.rotation;
        }

        currentInteractable = null;
        DoorInteractionHandler.instance.DoorInteractionTriggerExit(null);

        yield return new WaitForEndOfFrame();

        characterController.enabled = true;

        yield return new WaitForEndOfFrame();
        Player.instance.OnPlayerTelported?.Invoke();

        yield return new WaitForSeconds(1f);

        //Play Door Close SFX
        AudioClip closeClip = doorInteractable.customCloseClip != null ? doorInteractable.customCloseClip : doorCloseClip;
        audioSource.PlayOneShot(closeClip);

        yield return new WaitForSeconds(1f);

        //Interacted Success Event
        if (doorInteractable.OnInteractedSuccess != null)
            doorInteractable.OnInteractedSuccess.Invoke();

        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        busy = false;

        //Fade In
        FadeScreenUI.instance.FadeIn();
    }

    //On Player Enter Door Interaction Trigger
    //Show Interaction UI
    public void DoorInteractionTriggerEnter(DoorInteractable doorInteractable, DoorTriggerType doorTriggerType)
    {
        currentInteractable = doorInteractable;
        currentDoorTriggerType = doorTriggerType;

        //interactionPanel.gameObject.SetActive(true);
        //interactionPanel.transform.position = doorInteractable.interactPoint.position;
        //interactionCanvasGroup.alpha = 0f;

        //sequence = DOTween.Sequence();
        //sequence.Append(interactionCanvasGroup.DOFade(1f, 0.5f).SetEase(Ease.Linear));
        //sequence.ForceInit();
    }

    //On Player Exit Door Interaction Trigger
    //Disable Interaction UI
    public void DoorInteractionTriggerExit(DoorInteractable doorInteractable)
    {
        currentInteractable = null;

        //if (sequence != null)
        //    sequence.Kill();

        //interactionCanvasGroup.alpha = 0f;
        //interactionPanel.gameObject.SetActive(false);
    }
}
