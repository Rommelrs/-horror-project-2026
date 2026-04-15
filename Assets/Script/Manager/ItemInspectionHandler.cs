using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ItemInspectionHandler : MonoBehaviour
{
    public static ItemInspectionHandler instance;

    [SerializeField] InputActionReference interactionInput;
    [SerializeField] InputActionReference readInput;

    [SerializeField] GameObject inspectionCameraObj;
    [SerializeField] GameObject interactionPanel;
    [SerializeField] CanvasGroup interactionCanvasGroup;
    [SerializeField] MenuPanelSwitcher menuPanelSwitcher;
    [SerializeField] GameObject inspectionMenu;
    //[SerializeField] TMPro.TMP_Text interactTxt;

    [SerializeField] Transform itemRoot;
    [SerializeField] float itemRootRotateSpeed = 1f;
    [SerializeField] GameObject []inspectionItemObjects;

    [SerializeField] GameObject readHint;
    [SerializeField] GameObject[] readNoteUIs;
    [SerializeField] AudioClip noteInspectionClip;

    InspectableItemPickup currentInspectableItem;
    List<InspectableItemPickup> itemsInRange = new List<InspectableItemPickup>();
    DG.Tweening.Sequence sequence;

    string lastInspectedItem;
    bool isOpenedThroughInventory;
    public bool IsOpenedThroughInventory => isOpenedThroughInventory;
    bool rotateItem = true;

    public delegate void OnCloseInspection(string inspectedItemName);
    public OnCloseInspection onCloseInspection;

    Item currentItem;
    AudioSource audioSource;

    private void Awake()
    {
        instance = this;
        audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        interactionInput.action.Enable();
        readInput.action.Enable();
    }

    private void OnDisable()
    {
        interactionInput.action.Disable();
        readInput.action.Disable();
    }

    private void OnDestroy()
    {
        interactionInput.action.performed -= OnInteractButtonPressed;
        readInput.action.performed -= OnReadButtonPressed;
    }

    private void Start()
    {
        //Subscribe
        interactionInput.action.performed += OnInteractButtonPressed;
        readInput.action.performed += OnReadButtonPressed;
    }

    private void Update()
    {
        if (InspectionMenuIsActive() && rotateItem)
        {
            //Rotate Item Root
            if (itemRoot != null)
                itemRoot.transform.Rotate(Vector3.up * itemRootRotateSpeed * Time.unscaledDeltaTime, Space.Self);
        }
    }

    public void OnInteractButtonPressed(InputAction.CallbackContext callbackContext)
    {
        if (SubtitleManager.instance != null && (SubtitleManager.instance.IsSubtitleBusy() || SubtitleManager.instance.IsWithinCooldownPeriod()))
            return;

        if (GameManager.IsPaused)
            return;

        //On Interact Button pressed
        if (callbackContext.performed)
        {
            if (currentInspectableItem != null)
            {
                if (currentInspectableItem.itemToPickup != null)
                {
                    InspectItem(currentInspectableItem.itemToPickup);

                    //Add item to inventory
                    Player.instance.inventory.AddItem(currentInspectableItem.itemToPickup, currentInspectableItem.itemQuantity);

                    
                }

                //Play SFX
                if (currentInspectableItem.pckupClip != null)
                    SoundEffectManager.instance.PlaySFX(currentInspectableItem.pckupClip);

                //Set Interaction Time
                Player.instance.SetLastInteractionTime(Time.unscaledTime);

                //Interaction Success
                if (currentInspectableItem.OnInteracted != null)
                    currentInspectableItem.OnInteracted.Invoke();

                //Destory Object
                if (currentInspectableItem.destroyOnInteract)
                {
                    InspectableItemPickup itemToRemove = currentInspectableItem;
                    
                    // Mark as picked up for save system
                    SaveablePickup saveablePickup = currentInspectableItem.GetComponent<SaveablePickup>();
                    if (saveablePickup != null)
                    {
                        saveablePickup.MarkAsPickedUp();
                    }
                    
                    Destroy(currentInspectableItem.gameObject);
                    InspectableItemTriggerExit(itemToRemove);
                }
            }
        }
    }

    public void InspectItem(Item item, bool openedThroughInventory = false)
    {
        this.currentItem = item;

        isOpenedThroughInventory = openedThroughInventory;

        if(item != null && item.itemType != ItemType.Note)
        {
            //Enable Rotate
            this.rotateItem = true;
        }
        else
        {
            this.rotateItem = false;
        }


        if (this.rotateItem == false)
            itemRoot.localRotation = Quaternion.identity;

        isReading = false;
        DisableAllReadNoteUI();


        if (item != null && item.itemType == ItemType.Note)
            readHint.SetActive(true);
        else
            readHint.SetActive(false);

        //Play Note Pickup Clip
        if(item != null && item.itemType == ItemType.Note)
        {
            audioSource.PlayOneShot(noteInspectionClip);
        }

        //Enable Inspection Menu
        menuPanelSwitcher.SwitchPanel(inspectionMenu);

        ////Update Inspection Title
        //interactTxt.text = inspectTitle;

        //Enable Inspection Camera
        inspectionCameraObj.gameObject.SetActive(true);

        //Show Inspected Item
        for (int i = 0; i < inspectionItemObjects.Length; i++)
        {
            if (item != null && inspectionItemObjects[i].name.Equals(item.itemName, System.StringComparison.OrdinalIgnoreCase))
                inspectionItemObjects[i].gameObject.SetActive(true);
            else
                inspectionItemObjects[i].gameObject.SetActive(false);
        }

        //Set Item Name
        if (item != null)
            lastInspectedItem = item.itemName;
        else
            lastInspectedItem = string.Empty;

        //Pause the Game
        GameManager.IsPaused = true;
    }

    public void CloseInspectionMenu()
    {
        //Hide Inspection Items
        for (int i = 0; i < inspectionItemObjects.Length; i++)
        {
            inspectionItemObjects[i].SetActive(false);
        }

        //Disable Inspection Camera
        inspectionCameraObj.gameObject.SetActive(false);

        //Disable Inspection Menu
        inspectionMenu.SetActive(false);

        //Trigger Event
        onCloseInspection?.Invoke(lastInspectedItem);

        //Resume the Game
        GameManager.IsPaused = false;

        currentItem = null;
    }

    public bool InspectionMenuIsActive()
    {
        return inspectionMenu != null && inspectionMenu.activeSelf;
    }

    public void InspectableItemTriggerEnter(InspectableItemPickup inspectableItemPickup)
    {
        // Add to list if not already in it
        if (!itemsInRange.Contains(inspectableItemPickup))
        {
            itemsInRange.Add(inspectableItemPickup);
        }
        
        // Set as current (most recently entered)
        currentInspectableItem = inspectableItemPickup;
    }

    public void InspectableItemTriggerExit(InspectableItemPickup inspectableItemPickup)
    {
        // Remove from list
        itemsInRange.Remove(inspectableItemPickup);
        
        // If the exiting item was the current one, switch to another item in range
        if (currentInspectableItem == inspectableItemPickup)
        {
            // Clean up null entries
            itemsInRange.RemoveAll(item => item == null);
            
            if (itemsInRange.Count > 0)
            {
                // Switch to the most recent item still in range
                currentInspectableItem = itemsInRange[itemsInRange.Count - 1];
            }
            else
            {
                // No items left in range
                currentInspectableItem = null;
                
                //Reset Animation
                if (sequence != null)
                    sequence.Kill();

                //Disable Interaction Panel
                interactionCanvasGroup.alpha = 0f;
                interactionPanel.gameObject.SetActive(false);
            }
        }
    }

    bool isReading = false;

    public void OnReadButtonPressed(InputAction.CallbackContext callbackContext)
    {
        if (currentItem == null)
            return;

        if (currentItem.itemType == ItemType.Note)
        {
            if (isReading)
            {
                //Close Reading
                CancelReading();
            }
            else
            {
                //Enable Reading
                isReading = true;

                for (int i = 0; i < readNoteUIs.Length; i++)
                {
                    if (readNoteUIs[i].name == currentItem.itemName)
                        readNoteUIs[i].SetActive(true);
                    else
                        readNoteUIs[i].SetActive(false);
                }

                readHint.SetActive(false);
            }
        }
    }

    public bool IsNoteAndReading()
    {
        return ((currentItem != null && currentItem.itemType == ItemType.Note) && isReading);
    }

    public void CancelReading()
    {
        DisableAllReadNoteUI();

        readHint.SetActive(true);

        isReading = false;
    }

    void DisableAllReadNoteUI()
    {
        for (int i = 0; i < readNoteUIs.Length; i++)
            readNoteUIs[i].SetActive(false);
    }
}



