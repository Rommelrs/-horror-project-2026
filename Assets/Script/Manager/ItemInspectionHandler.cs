using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    [Tooltip("\"ESC Close\" hint, shown while any item is being inspected (including the full-screen note reader).")]
    [SerializeField] GameObject exitHint;
    [Tooltip("Vertical gap between the \"F Read\" hint and the exit hint under it.")]
    [SerializeField] float exitHintSpacing = 58f;
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

        StartCoroutine(Co_HideInspectionCamera());
    }

    // The inspection camera starts active so its PSXShaderManager.Start runs at scene start like it always has (the
    // Main Camera's manager runs after it and sets the PSX shader globals); one frame later it's turned off so it only
    // renders while an item is inspected or shown in the inventory
    IEnumerator Co_HideInspectionCamera()
    {
        yield return null;
        if (!InspectionMenuIsActive())
            inspectionCameraObj.SetActive(false);
    }

    private void Update()
    {
        if (InspectionMenuIsActive() && rotateItem)
        {
            //Rotate Item Root
            if (itemRoot != null)
                itemRoot.transform.Rotate(Vector3.up * itemRootRotateSpeed * Time.unscaledDeltaTime, Space.Self);
        }

        // a subtitle ("I got 2 ammo", the note on the knife handle) runs across the bottom of the screen: the Read / Close hints
        // sit above it instead of being written over. Decided once per inspection and never animated - the pickup's line starts
        // in the same frame the item opens, so the hints simply appear in the raised spot and stay there until it closes.
        if (InspectionMenuIsActive() && SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy())
            hintsRaised = true;
        hintLift = hintsRaised ? subtitleHintLift : 0f;
        PositionExitHint();
    }

    [Tooltip("How far the Read / Close hints rise while a subtitle is on screen (canvas pixels).")]
    [SerializeField] float subtitleHintLift = 230f;
    float hintLift;
    bool hintsRaised;
    float inspectionOpenedAt = -10f;

    [Tooltip("Key shown on the Close hint for items (they close with interact). Notes keep the hint's own Esc key.")]
    [SerializeField] Sprite interactKeyIcon;
    Sprite escKeyIcon;
    Image exitKeyImage;

    void SetExitKeyIcon(bool note)
    {
        if (exitKeyImage == null)
        {
            var t = exitHint.transform.Find("Image");
            if (t == null) return;
            exitKeyImage = t.GetComponent<Image>();
            if (exitKeyImage == null) return;
            escKeyIcon = exitKeyImage.sprite;
        }
        var sprite = note || interactKeyIcon == null ? escKeyIcon : interactKeyIcon;
        if (sprite == null) return;
        exitKeyImage.sprite = sprite;
        var rt = (RectTransform)exitKeyImage.transform;
        float h = rt.sizeDelta.y;
        rt.sizeDelta = new Vector2(h * sprite.rect.width / sprite.rect.height, h);
    }
    Vector2 readHintBase;
    bool haveReadHintBase;

    public void OnInteractButtonPressed(InputAction.CallbackContext callbackContext)
    {
        if (PlayerCloseCombat.instance != null && PlayerCloseCombat.instance.BlocksInteract) return;   // held by a Questioner: E is the escape sliders' key
        if (SubtitleManager.instance != null && (SubtitleManager.instance.IsSubtitleBusy() || SubtitleManager.instance.IsWithinCooldownPeriod()))
            return;

        // An item (not a note) on the inspect screen closes with the interact key too - the hint shows [E] Close for them.
        // A short guard so the press that opened it doesn't also close it. Notes keep Esc (and F to read).
        if (callbackContext.performed && InspectionMenuIsActive() && currentItem != null && currentItem.itemType != ItemType.Note
            && !isReading && Time.unscaledTime > inspectionOpenedAt + 0.25f)
        {
            if (IsOpenedThroughInventory && InventoryUI.instance != null) InventoryUI.instance.EnableInventoryUI(true);
            else CloseInspectionMenu();
            if (Player.instance != null) Player.instance.SetLastInteractionTime(Time.unscaledTime);
            return;
        }

        if (GameManager.IsPaused)
            return;

        //On Interact Button pressed
        if (callbackContext.performed)
        {
            if (currentInspectableItem != null)
            {
                if (currentInspectableItem.itemToPickup != null)
                {
                    if (!currentInspectableItem.instantPickup)
                        InspectItem(currentInspectableItem.itemToPickup);

                    //Add item to inventory
                    Player.instance.inventory.PickUp(currentInspectableItem.itemToPickup, currentInspectableItem.itemQuantity);

                    
                }

                //Play SFX
                if (currentInspectableItem.pckupClip != null)
                    SoundEffectManager.instance.PlaySFX(currentInspectableItem.pckupClip);
                // No clip of its own: the main menu hover, so no item is picked up in silence. Notes stay silent.
                else if (currentInspectableItem.itemToPickup != null && currentInspectableItem.itemToPickup.itemType != ItemType.Note && UISoundManager.instance != null)
                    UISoundManager.instance.PlayButtonHoverSound();

                //Set Interaction Time
                Player.instance.SetLastInteractionTime(Time.unscaledTime);

                // Register the pickup with the save system BEFORE firing OnInteracted, since that event
                // can trigger a checkpoint - which must see this item as picked up, not just in the inventory.
                if (currentInspectableItem.destroyOnInteract)
                {
                    SaveablePickup saveablePickup = currentInspectableItem.GetComponent<SaveablePickup>();
                    if (saveablePickup != null)
                    {
                        saveablePickup.MarkAsPickedUp();
                    }
                }

                // Instant pickups skip their own "I got a ..." subtitle; the rest of OnInteracted (checkpoints etc.) still runs.
                // Removing the component makes UnityEvent skip that persistent call.
                if (currentInspectableItem.instantPickup)
                {
                    SubtitleTrigger ownSubtitle = currentInspectableItem.GetComponent<SubtitleTrigger>();
                    if (ownSubtitle != null)
                        DestroyImmediate(ownSubtitle);
                }

                //Interaction Success
                if (currentInspectableItem.OnInteracted != null)
                    currentInspectableItem.OnInteracted.Invoke();

                //Destory Object
                if (currentInspectableItem.destroyOnInteract)
                {
                    InspectableItemPickup itemToRemove = currentInspectableItem;

                    Destroy(currentInspectableItem.gameObject);
                    InspectableItemTriggerExit(itemToRemove);
                }
            }
        }
    }

    public void InspectItem(Item item, bool openedThroughInventory = false)
    {
        this.currentItem = item;
        hintsRaised = false;
        inspectionOpenedAt = Time.unscaledTime;

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


        if (item != null && item.itemType == ItemType.Note && item.showReadPrompt)
            readHint.SetActive(true);
        else
            readHint.SetActive(false);

        // a note that opens straight on its page (the credit book) - unless it is too dark in here to read it
        bool readNow = false;
        if (item != null && item.itemType == ItemType.Note && item.readOnOpen && item.showReadPrompt)
        {
            if (TooDark(item)) { readHint.SetActive(false); SayTooDark(item); }
            else readNow = true;
        }

        //Let the player know ESC closes the inspection
        if (exitHint != null)
        {
            exitHint.SetActive(item != null);
            SetExitKeyIcon(item != null && item.itemType == ItemType.Note);
        }
        PositionExitHint();

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

        if (readNow) BeginReading();
    }

    static bool TooDark(Item item) => item != null && item.needsLightToRead && StoreBlackoutSequence.TooDarkToRead;

    Coroutine tooDarkCR;
    void SayTooDark(Item item)
    {
        if (tooDarkCR != null) StopCoroutine(tooDarkCR);
        tooDarkCR = StartCoroutine(Co_SayTooDark(item));
    }

    // a moment after the book opens: he tries to make it out, then gives up
    IEnumerator Co_SayTooDark(Item item)
    {
        yield return new WaitForSecondsRealtime(0.7f);
        tooDarkCR = null;
        if (currentItem != item || !InspectionMenuIsActive() || item.tooDarkLine == null || item.tooDarkLine.IsEmpty) yield break;
        SubtitleManager.ShowSubtitle(item.tooDarkLine, false);
    }

    void BeginReading()
    {
        isReading = true;
        for (int i = 0; i < readNoteUIs.Length; i++)
            readNoteUIs[i].SetActive(readNoteUIs[i].name == currentItem.itemName);
        readHint.SetActive(false);
        PositionExitHint();
    }

    public void CloseInspectionMenu()
    {
        hintsRaised = false;
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

        if (currentItem.itemType == ItemType.Note && currentItem.showReadPrompt)
        {
            if (isReading)
            {
                //Close Reading
                CancelReading();
            }
            else if (TooDark(currentItem))
            {
                if (SubtitleManager.instance == null || !SubtitleManager.instance.IsSubtitleBusy()) SayTooDark(currentItem);
            }
            else
            {
                //Enable Reading
                BeginReading();
            }
        }
    }

    // The exit hint sits right under "F Read"; when "F Read" is hidden it moves up into its slot
    void PositionExitHint()
    {
        if (exitHint == null || readHint == null)
            return;

        RectTransform exitRect = (RectTransform)exitHint.transform;
        RectTransform readRect = (RectTransform)readHint.transform;
        if (!haveReadHintBase) { readHintBase = readRect.anchoredPosition; haveReadHintBase = true; }
        Vector2 lift = new Vector2(0f, hintLift);
        readRect.anchoredPosition = readHintBase + lift;
        exitRect.anchoredPosition = readHintBase + lift + (readHint.activeSelf ? new Vector2(0f, -exitHintSpacing) : Vector2.zero);
    }

    public bool IsNoteAndReading()
    {
        return ((currentItem != null && currentItem.itemType == ItemType.Note) && isReading);
    }

    public void CancelReading()
    {
        DisableAllReadNoteUI();

        readHint.SetActive(true);
        PositionExitHint();

        isReading = false;
    }

    void DisableAllReadNoteUI()
    {
        for (int i = 0; i < readNoteUIs.Length; i++)
            readNoteUIs[i].SetActive(false);
    }
}



