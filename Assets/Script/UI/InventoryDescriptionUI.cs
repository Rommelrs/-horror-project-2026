using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

public class InventoryDescriptionUI : MonoBehaviour
{
    [SerializeField] RawImage itemRawImage;
    [SerializeField] Text noItemFoundTxt;

    [SerializeField] Text itemNameTxt;
    [SerializeField] Text itemStockTxt;
    [SerializeField] Text descriptionTxt;

    [SerializeField] Button useButton;
    [SerializeField] Button inspectButton;
    [SerializeField] Button readButton;

    [SerializeField] AudioClip itemUseClip;

    [SerializeField] GameObject inspectionCameraObj;
    [SerializeField] Transform itemRoot;
    [SerializeField] float itemRootRotateSpeed = 1f;
    [SerializeField] GameObject[] inspectionItemObjects;

    [SerializeField] LocalizedString nameString;
    [SerializeField] LocalizedString stockString;

    Item currentItem;
    AudioSource audioSource;

    bool rotateItem = true;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        //Subscribe to Buttons
        if(useButton != null)
            useButton.onClick.AddListener(OnUseButtonClicked);

        if (inspectButton != null)
            inspectButton.onClick.AddListener(OnInspectButtonClicked);

        if (readButton != null)
            readButton.onClick.AddListener(OnReadButtonClicked);
    }

    private void Update()
    {
        if (currentItem != null && rotateItem && InventoryUI.instance && InventoryUI.instance.InventoryIsActive())
        {
            //Rotate Item Root
            if (itemRoot != null)
                itemRoot.transform.Rotate(Vector3.up * itemRootRotateSpeed * Time.unscaledDeltaTime, Space.Self);
        }
    }

    public void ShowItemDescription(Item item, int itemStack)
    {
        //Update Current Item
        currentItem = item;

        if (item != null)
        {
            if (item.itemType == ItemType.Note)
                rotateItem = false;
            else
                rotateItem = true;

            if (rotateItem == false)
                itemRoot.localRotation = Quaternion.identity;

            ShowItem3DIcon();

            noItemFoundTxt.gameObject.SetActive(false);

            //Update description information
            if (item.itemType == ItemType.Note)
            {
                //Notes

                itemNameTxt.text = nameString.GetLocalizedString() + " " + getLocalString("GameUI", item.itemName);
                itemStockTxt.text = getLocalString("GameUI", item.itemName + " Description" );
                descriptionTxt.text = string.Empty;
            }
            else
            {
                //Items
                itemNameTxt.text = nameString.GetLocalizedString() + " " + getLocalString("GameUI", item.itemName);
                itemStockTxt.text = stockString.GetLocalizedString() + " " + itemStack.ToString();
                descriptionTxt.text = getLocalString("GameUI", item.itemName + " Description");
            }

            CheckIfButtonIsUseable(item);
        }
        else
        {
            rotateItem = false;
            HideItem3DIcon();

            noItemFoundTxt.gameObject.SetActive(true);

            //Hide description Menu
            itemNameTxt.text = string.Empty;
            itemStockTxt.text = string.Empty;
            descriptionTxt.text = string.Empty;

            //Hide Buttons
            useButton.gameObject.SetActive(false);
            inspectButton.gameObject.SetActive(false);
            readButton.gameObject.SetActive(false);
        }
    }

    private string m;

    string getLocalString(string table, string key)
    {
        // Sometimes the localized value may not be immediately available.
        // The Localization system may not have been initialized yet or the String Table may need loading.
        // The AsyncOperation wraps this loading operation. We can yield on it in a coroutine,
        // use its various Completed Events or await its Task if using async and await.
        var stringOperation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, key);
        if (stringOperation.IsDone)
        {
            SetString(stringOperation);
            return m;
        }

        StartCoroutine(LoadStringWithCoroutine(stringOperation));
        return m;
    }

    IEnumerator LoadStringWithCoroutine(AsyncOperationHandle<string> stringOperation)
    {
        yield return stringOperation;
        SetString(stringOperation);
    }

    void SetString(AsyncOperationHandle<string> stringOperation)
    {
        // Its possible that something may have gone wrong during loading. We can handle this locally
        // or ignore all errors as they will still be captured and reported by the Localization system.
        if (stringOperation.Status == AsyncOperationStatus.Failed)
            m = "Failed to load string";
        else
            m = stringOperation.Result;
    }

    string GetLocalizedString(StringTable table, string entryName)
    {
        // Get the table entry. The entry contains the localized string and Metadata
        var entry = table.GetEntry(entryName);
        return entry.GetLocalizedString(); // We can pass in optional arguments for Smart Format or String.Format here
    }


    void CheckIfButtonIsUseable(Item item)
    {
        if(item == null)
        {
            useButton.gameObject.SetActive(false);
            inspectButton.gameObject.SetActive(false);
            readButton.gameObject.SetActive(false);
            return;
        }

        if(item.itemType == ItemType.Note)
        {
            useButton.gameObject.SetActive(false);
            inspectButton.gameObject.SetActive(false);
            readButton.gameObject.SetActive(true);
            return;
        }
        else
        {
            inspectButton.gameObject.SetActive(true);
            readButton.gameObject.SetActive(false);
        }

        //Show Buttons based on item type
        if (item.itemType == ItemType.Healing 
            || item.itemType == ItemType.EnergyDrink 
            || item.itemType == ItemType.AddStability
            || item.itemType == ItemType.HealingAndAddStability
            || item.itemType == ItemType.Bandage
            || item.itemType == ItemType.CalmingInhaler
            || (item.itemType == ItemType.Fuse && Player.instance.fuseBoxInRange)
            || (item.itemType == ItemType.UVLight && ImageFadeInteractable.currentInRange != null && ImageFadeInteractable.currentInRange.SupportsUVLight())
            || (item.itemType == ItemType.DuctTape && WallHoleInteractable.currentInRange != null && WallHoleInteractable.currentInRange.CanApplyTape())
            || (item.itemType == ItemType.Knife && BurnableObstacle.currentInRange != null && BurnableObstacle.currentInRange.CanUseLighter())
            || (item.itemType == ItemType.Lighter && BurnableObstacle.currentInRange != null && BurnableObstacle.currentInRange.CanUseLighter())
            || (item.itemType == ItemType.WoodenPlank && BurnableObstacle.currentInRange != null && BurnableObstacle.currentInRange.CanUseLighter()))
        {
            useButton.gameObject.SetActive(true);
        }
        else
        {
            useButton.gameObject.SetActive(false);
        }

        // Debug for UV Light
        if (item.itemType == ItemType.UVLight)
        {
            Debug.Log("UV Light check - currentInRange: " + (ImageFadeInteractable.currentInRange != null) + " | SupportsUV: " + (ImageFadeInteractable.currentInRange != null ? ImageFadeInteractable.currentInRange.SupportsUVLight().ToString() : "N/A"));
        }

        // Debug for Duct Tape
        if (item.itemType == ItemType.DuctTape)
        {
            Debug.Log("DuctTape check - currentInRange: " + (WallHoleInteractable.currentInRange != null) + " | CanApplyTape: " + (WallHoleInteractable.currentInRange != null ? WallHoleInteractable.currentInRange.CanApplyTape().ToString() : "N/A"));
        }
    }

    void ShowItem3DIcon()
    {
        //Enable Inspection Camera
        inspectionCameraObj.gameObject.SetActive(true);
        itemRawImage.gameObject.SetActive(true);

        //Show Inspected Item
        for (int i = 0; i < inspectionItemObjects.Length; i++)
        {
            if (inspectionItemObjects[i].gameObject.name == currentItem.itemName)
                inspectionItemObjects[i].gameObject.SetActive(true);
            else
                inspectionItemObjects[i].gameObject.SetActive(false);
        }
    }

    void HideItem3DIcon()
    {
        inspectionCameraObj.gameObject.SetActive(false);
        itemRawImage.gameObject.SetActive(false);
    }

    public void OnUseButtonClicked()
    {
        if(currentItem != null && currentItem.itemType == ItemType.Healing)
        {
            //Heal Player
            Player.instance.health.Heal(currentItem.healingAmount);

            //Play SFX
            audioSource.PlayOneShot(itemUseClip);

            //Remove Item from inventory
            Player.instance.inventory.RemoveItem(currentItem);

            //Uupdate Inventory UI
            InventoryUI.instance.EnableInventoryUI(true);
        }

        if (currentItem != null && currentItem.itemType == ItemType.HealingAndAddStability)
        {
            //Heal Player
            Player.instance.health.Heal(currentItem.healingAmount);

            //Add Stability
            Player.instance.playerStability.IncreaseStability(currentItem.stabilityIncreaseAmount);

            //Play SFX
            audioSource.PlayOneShot(itemUseClip);

            //Remove Item from inventory
            Player.instance.inventory.RemoveItem(currentItem);

            //Uupdate Inventory UI
            InventoryUI.instance.EnableInventoryUI(true);
        }

        if (currentItem != null && currentItem.itemType == ItemType.Bandage)
        {
            //Heal Player
            if(Player.instance.health.GetHealthValue() < 50)
            {
                int amountToHeal = 50 - Player.instance.health.GetHealthValue();
                Player.instance.health.Heal(amountToHeal);
            }

            //Play SFX
            audioSource.PlayOneShot(itemUseClip);

            //Remove Item from inventory
            Player.instance.inventory.RemoveItem(currentItem);

            //Uupdate Inventory UI
            InventoryUI.instance.EnableInventoryUI(true);
        }

        if (currentItem != null && currentItem.itemType == ItemType.AddStability)
        {
            //Add Stability
            Player.instance.playerStability.IncreaseStability(currentItem.stabilityIncreaseAmount);

            //Play SFX
            audioSource.PlayOneShot(itemUseClip);

            //Remove Item from inventory
            Player.instance.inventory.RemoveItem(currentItem);

            //Uupdate Inventory UI
            InventoryUI.instance.EnableInventoryUI(true);
        }

        if (currentItem != null && currentItem.itemType == ItemType.EnergyDrink)
        {
            //Play SFX
            audioSource.PlayOneShot(itemUseClip);

            //Remove Item from inventory
            Player.instance.inventory.RemoveItem(currentItem);

            //Uupdate Inventory UI
            InventoryUI.instance.EnableInventoryUI(true);
        }

        if (currentItem != null && currentItem.itemType == ItemType.CalmingInhaler)
        {
            //Activate Calming Inhaler
            Player.instance.playerStability.ActivateCalmingInhaler(currentItem.calmingInhalerDuration);

            //Play SFX
            audioSource.PlayOneShot(itemUseClip);

            //Remove Item from inventory
            Player.instance.inventory.RemoveItem(currentItem);

            //Uupdate Inventory UI
            InventoryUI.instance.EnableInventoryUI(true);
        }

        if (currentItem != null && currentItem.itemType == ItemType.Fuse)
        {
            //Disable Inventory Menu
            InventoryUI.instance.DisableInventoryMenu();

            //Remove Item from inventory
            Player.instance.inventory.RemoveItem(currentItem);

            currentItem = null;

            if (Player.instance.fuseBoxInRange && Player.instance.currentFuseboxInRange)
            {
                //Use Fuse Item
                Player.instance.currentFuseboxInRange.UseFuse();
            }
        }

        if (currentItem != null && currentItem.itemType == ItemType.UVLight)
        {
            if (ImageFadeInteractable.currentInRange != null && ImageFadeInteractable.currentInRange.SupportsUVLight())
            {
                //Play SFX
                audioSource.PlayOneShot(itemUseClip);

                //Hide Inventory instantly (no fade, UV Light will handle the fade)
                InventoryUI.instance.HideInventoryInstant();

                //Use UV Light (this triggers the fade)
                ImageFadeInteractable.currentInRange.UseUVLight();
            }
        }

        if (currentItem != null && currentItem.itemType == ItemType.DuctTape)
        {
            if (WallHoleInteractable.currentInRange != null && WallHoleInteractable.currentInRange.CanApplyTape())
            {
                //Play SFX
                audioSource.PlayOneShot(itemUseClip);

                //Hide Inventory instantly (no fade, ApplyTape will handle the fade)
                InventoryUI.instance.HideInventoryInstant();

                //Remove Item from inventory
                Player.instance.inventory.RemoveItem(currentItem);

                //Apply duct tape (this triggers the fade)
                WallHoleInteractable.currentInRange.ApplyTape();
            }
        }
        
        if (currentItem != null && currentItem.itemType == ItemType.Knife)
        {
            if (BurnableObstacle.currentInRange != null && BurnableObstacle.currentInRange.CanUseLighter())
            {
                //Play SFX
                audioSource.PlayOneShot(itemUseClip);

                //Disable Inventory Menu
                InventoryUI.instance.DisableInventoryMenu();

                //Use knife (knife is NOT consumed, reusable)
                BurnableObstacle.currentInRange.BurnFromInventory(ItemType.Knife);
            }
        }
        
        if (currentItem != null && currentItem.itemType == ItemType.Lighter)
        {
            if (BurnableObstacle.currentInRange != null && BurnableObstacle.currentInRange.CanUseLighter())
            {
                //Play SFX
                audioSource.PlayOneShot(itemUseClip);

                //Disable Inventory Menu
                InventoryUI.instance.DisableInventoryMenu();

                //Burn obstacle (lighter is not consumed)
                BurnableObstacle.currentInRange.BurnFromInventory(ItemType.Lighter);
            }
        }
        
        if (currentItem != null && currentItem.itemType == ItemType.WoodenPlank)
        {
            if (BurnableObstacle.currentInRange != null && BurnableObstacle.currentInRange.CanUseLighter())
            {
                //Play SFX
                audioSource.PlayOneShot(itemUseClip);

                //Disable Inventory Menu
                InventoryUI.instance.DisableInventoryMenu();

                //Remove Item from inventory
                Player.instance.inventory.RemoveItem(currentItem);

                //Use wooden plank (place it on bridge/obstacle)
                BurnableObstacle.currentInRange.BurnFromInventory(ItemType.WoodenPlank);
            }
        }
    }

    public void OnInspectButtonClicked()
    {
        ItemInspectionHandler.instance.InspectItem(currentItem, true);
        currentItem = null;
    }

    public void OnReadButtonClicked()
    {
        ItemInspectionHandler.instance.InspectItem(currentItem, true);
        currentItem = null;
    }
}
