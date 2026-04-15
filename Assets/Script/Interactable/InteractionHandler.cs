using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionHandler : MonoBehaviour
{
    public static InteractionHandler instance;

    [SerializeField] InputActionReference interactionInput;
    [SerializeField] GameObject interactionPanel;

    [SerializeField] CanvasGroup interactCanvasGroup;
    [SerializeField] CanvasGroup pickupCanvasGroup;

    [SerializeField] AudioClip []itemPickupClips;

    List<Interactable> currentInteractableList;
    Interactable currentInteractable;

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
        currentInteractableList = new List<Interactable>();

        //Subscribe to interaction input
        interactionInput.action.performed += OnInteractButtonPressed;
    }

    public void OnInteractButtonPressed(InputAction.CallbackContext callbackContext)
    {
        if (SubtitleManager.instance && (SubtitleManager.instance.IsSubtitleBusy() || SubtitleManager.instance.IsWithinCooldownPeriod()))
            return;

        if (GameManager.IsPaused)
            return;

        if (ItemInspectionHandler.instance && ItemInspectionHandler.instance.InspectionMenuIsActive())
            return;

        //On Interact Button pressed
        if (callbackContext.performed)
        {
            if (currentInteractable != null)
            {
                if(currentInteractable is DrillHoleInteractable)
                {
                    DrillHoleInteractable drillHoleInteractable = (DrillHoleInteractable)currentInteractable;
                    if (drillHoleInteractable.HasDrill())
                    {
                        int drillCharge = drillHoleInteractable.DrillChargeCount();
                        if(drillCharge > 0)
                        {
                            //Has Drill Charge
                        }
                        else
                        {
                            //Has no Drill Charge
                            //Debug.LogError("Has no Drill Charge!");

                            if (MessageHandler.instance)
                            {
                                MessageHandler.instance.ShowMessage("Has no Drill Charge!");
                            }

                            return;
                        }
                    }
                    else
                    {
                        //Debug.LogError("Has no Drill!");

                        if (MessageHandler.instance)
                        {
                            MessageHandler.instance.ShowMessage("Has no Drill!");
                        }

                        return;
                    }
                }

                //Interacted
                currentInteractable.Interacted();

                //Set last Interaction Time
                if(Player.instance)
                    Player.instance.SetLastInteractionTime(Time.unscaledTime);

                //Interaction Success
                if (currentInteractable.OnInteracted != null)
                    currentInteractable.OnInteracted.Invoke();

                //Destory Object
                if (currentInteractable.destroyOnInteract)
                {
                    // Mark as picked up for save system
                    SaveablePickup saveablePickup = currentInteractable.GetComponent<SaveablePickup>();
                    if (saveablePickup != null)
                    {
                        saveablePickup.MarkAsPickedUp();
                    }
                    
                    Destroy(currentInteractable.gameObject);
                    InspectableItemTriggerExit(null);
                }
                else
                {
                    InspectableItemTriggerExit(currentInteractable);
                }            
            }
        }
    }

    public void PlayItemPickupSound()
    {
        //Play Item Pickup Sound
        if (audioSource != null && itemPickupClips != null && itemPickupClips.Length > 0)
            audioSource.PlayOneShot(itemPickupClips[Random.Range(0, itemPickupClips.Length)]);
    }

    //Try to set new target interactable from the current interactable list
    void AttempToTargetCurrentInteractable()
    {
        StartCoroutine(Co_AttempToTargetCurrentInteractable());
    }

    IEnumerator Co_AttempToTargetCurrentInteractable()
    {
        yield return new WaitForEndOfFrame();
     
        if (currentInteractableList != null && currentInteractableList.Count > 0)
        {
            //Clear all null objects
            currentInteractableList.RemoveAll(item => item == null);

            //Clear all disabled objects
            currentInteractableList.RemoveAll(item => !item.gameObject.activeInHierarchy);
        }

        if (currentInteractableList != null && currentInteractableList.Count > 0)
        {
            //Target First Interactable from the list
            TargetCurrentInteractable(currentInteractableList[0]);
        }
        else
        {
            //Hide Interaction UI
            currentInteractable = null;
            HideInteractionUI();
        }
    }

    void TargetCurrentInteractable(Interactable targetInteractable)
    {
        currentInteractable = targetInteractable;

        if(currentInteractable != null)
        {
            ////Enable Interaction Panel
            //interactionPanel.gameObject.SetActive(true);
            //interactionPanel.transform.position = currentInteractable.interactPoint.position;

            //interactCanvasGroup.alpha = 0f;
            //pickupCanvasGroup.alpha = 0f;

            ////Start New Animation Sequence
            //sequence = DOTween.Sequence();

            //if (currentInteractable.interactionType == InteractionType.Interact)
            //{
            //    sequence.Append(interactCanvasGroup.DOFade(1f, 0.5f).SetEase(Ease.Linear));
            //}
            //else if (currentInteractable.interactionType == InteractionType.Pickup)
            //{
            //    sequence.Append(pickupCanvasGroup.DOFade(1f, 0.5f).SetEase(Ease.Linear));
            //}

            //sequence.ForceInit();
        }
        else
        {
            HideInteractionUI();
        }
    }

    void HideInteractionUI()
    {
        //Reset Animation
        if (sequence != null)
            sequence.Kill();

        //Disable Interaction Panel
        interactCanvasGroup.alpha = 0f;
        pickupCanvasGroup.alpha = 0f;
        interactionPanel.gameObject.SetActive(false);
    }

    public void InspectableItemTriggerEnter(Interactable interactable)
    {
        //Update Current Inspectable Item
        if (!currentInteractableList.Contains(interactable))
            currentInteractableList.Add(interactable);

        //Try to target new interactable
        AttempToTargetCurrentInteractable();
    }

    public void InspectableItemTriggerExit(Interactable interactable)
    {
        //Remove Interactable from the list
        if (currentInteractableList != null && currentInteractableList.Contains(interactable))
            currentInteractableList.Remove(interactable);

        //Try to target new interactable
        AttempToTargetCurrentInteractable();
    }
}
