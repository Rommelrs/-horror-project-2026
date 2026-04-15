using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Localization;

public class InventoryUI : MonoBehaviour
{
    public static InventoryUI instance;

    [SerializeField] InputActionReference inventoryMenuInput;
    [SerializeField] GameObject inventoryMenu;
    [SerializeField] CanvasGroup inventoryCanvasGroup;
    [SerializeField] MenuPanelSwitcher menuPanelSwitcher;
    
    // Use Player.instance.inventory instead of serialized reference for persistence across scenes
    Inventory inventory => Player.instance != null ? Player.instance.inventory : null;
    [SerializeField] InventoryDescriptionUI inventoryDescriptionUI;
    [SerializeField] Button selectLeftButton;
    [SerializeField] Button selectRightButton;
    [SerializeField] Text conditionTxt;
    [SerializeField] Text stabilityTxt;

    List<Inventory.ItemStack> inventoryItems;
    int selectionIndex = 0;

    [System.Serializable]
    public enum InventoryCategory
    {
        Items,
        Notes
    }
    public InventoryCategory currentCategory = InventoryCategory.Items;
    [SerializeField] GameObject []categorySelectionObjs;
    [SerializeField] Button itemsCategoryButton;
    [SerializeField] Button notesCategoryButton;

    Coroutine openInventoryCR;

    private void Awake()
    {
        instance = this;
    }

    private void OnEnable()
    {
        inventoryMenuInput.action.Enable();
    }

    private void OnDisable()
    {
        inventoryMenuInput.action.Disable();
    }

    private void Start()
    {
        inventoryMenuInput.action.performed += OnInventoryButtonPressed;

        selectLeftButton.onClick.AddListener(() => SelectNextItem(-1));
        selectRightButton.onClick.AddListener(() => SelectNextItem(1));

        itemsCategoryButton.onClick.AddListener(() => SelectCategory(0));
        notesCategoryButton.onClick.AddListener(() => SelectCategory(1));
    }

    private void OnDestroy()
    {
        inventoryMenuInput.action.performed -= OnInventoryButtonPressed;
    }

    public bool InventoryIsActive()
    {
        return inventoryMenu.gameObject.activeSelf;
    }

    public void DisableInventoryMenu()
    {
        //Disable Inventory Menu
        inventoryMenu.gameObject.SetActive(false);

        //Resume the Game
        GameManager.IsPaused = false;

        if (openInventoryCR != null)
        {
            StopCoroutine(openInventoryCR);
            FadeScreenUI.instance.FadeIn();
        }

        inventoryCanvasGroup.alpha = 0f;
    }

    // Hide inventory without fade (for UV Light use)
    public void HideInventoryInstant()
    {
        inventoryMenu.gameObject.SetActive(false);
        
        if (openInventoryCR != null)
            StopCoroutine(openInventoryCR);

        inventoryCanvasGroup.alpha = 0f;
    }

    public void OnInventoryButtonPressed(InputAction.CallbackContext callbackContext)
    {
        if (LevelManager.instance.isGameOver || LevelManager.instance.isGameWon)
            return;

        if (callbackContext.performed)
        {
            if (inventoryMenu.gameObject.activeSelf)
            {
                //Disable Inventory Menu
                DisableInventoryMenu();
            }
            else
            {
                EnableInventoryUI();
            }
        }
    }

    public void EnableInventoryUI(bool instantEnable = false)
    {
        if (LevelManager.instance.isGameOver || LevelManager.instance.isGameWon)
            return;

        //Disable Inventory toggle if subtitle is playing
        if (SubtitleManager.instance && SubtitleManager.instance.IsSubtitleBusy())
            return;

        //If Item Inspection Menu is active then Close it
        if (ItemInspectionHandler.instance && ItemInspectionHandler.instance.InspectionMenuIsActive())
        {
            ItemInspectionHandler.instance.CloseInspectionMenu();
        }

        //If CashRegister Menu is active then Close it
        if (CashRegisterInteractable.instance && CashRegisterInteractable.instance.CashRegisterMenuIsActive())
        {
            CashRegisterInteractable.instance.CloseCashRegisterMenu();
        }

        //If Game is Paused then resume
        if (PauseHandler.instance.IsPaused)
            PauseHandler.instance.ResumeGame();

        //Enable Inventory Menu
        menuPanelSwitcher.SwitchPanel(inventoryMenu);


        UpdateStatusTexts();

        if(currentCategory == InventoryCategory.Items)
        {
            inventoryItems = inventory.GetItems();
            selectionIndex = Mathf.Clamp(selectionIndex, 0, inventoryItems.Count - 1);
            UpdateInventoryItemSelection();
        }
        else if(currentCategory == InventoryCategory.Notes)
        {
            inventoryItems = inventory.GetNotes();
            selectionIndex = Mathf.Clamp(selectionIndex, 0, inventoryItems.Count - 1);
            UpdateInventoryItemSelection();
        }

        //Resume the Game
        GameManager.IsPaused = true;

        if (instantEnable)
        {
            if (openInventoryCR != null) StopCoroutine(openInventoryCR);

            //Show
            inventoryCanvasGroup.alpha = 1f;
        }
        else
        {

            //Hide
            inventoryCanvasGroup.alpha = 0f;

            if (openInventoryCR != null) StopCoroutine(openInventoryCR);
            openInventoryCR = StartCoroutine(Co_EnableInventoryUI());
        }
    }

    IEnumerator Co_EnableInventoryUI()
    {
        FadeScreenUI.instance.FadeOut();
        yield return new WaitForSecondsRealtime(1f);

        inventoryCanvasGroup.alpha = 1f;
        FadeScreenUI.instance.FadeIn();
    }

    [SerializeField] LocalizedString conditionString;
    [SerializeField] LocalizedString healthyString;
    [SerializeField] LocalizedString goodString;
    [SerializeField] LocalizedString okString;
    [SerializeField] LocalizedString badString;
    [SerializeField] LocalizedString severeString;

    [SerializeField] LocalizedString stabilityString;
    [SerializeField] LocalizedString stableString;
    [SerializeField] LocalizedString unsteadyString;
    [SerializeField] LocalizedString shakenString;
    [SerializeField] LocalizedString disturbedString;
    [SerializeField] LocalizedString fracturedString;
    
    [Header("Health Status Colors")]
    [SerializeField] Color healthyColor = Color.green;
    [SerializeField] Color goodColor = new Color(0.5f, 1f, 0.5f); // Light green
    [SerializeField] Color okColor = Color.yellow;
    [SerializeField] Color badColor = new Color(1f, 0.5f, 0f); // Orange
    [SerializeField] Color severeColor = Color.red;
    
    [Header("Stability Status Colors")]
    [SerializeField] Color stableColor = Color.green;
    [SerializeField] Color unsteadyColor = new Color(0.5f, 1f, 0.5f); // Light green
    [SerializeField] Color shakenColor = Color.yellow;
    [SerializeField] Color disturbedColor = new Color(1f, 0.5f, 0f); // Orange
    [SerializeField] Color fracturedColor = Color.red;

    void UpdateStatusTexts()
    {
        int health = Player.instance.health.GetHealthValue();
        string conditionStr = string.Empty;
        Color conditionColor = Color.white;
        
        if (health >= 100)
        {
            conditionStr = healthyString.GetLocalizedString();
            conditionColor = healthyColor;
        }
        else if (health >= 60)
        {
            conditionStr = goodString.GetLocalizedString();
            conditionColor = goodColor;
        }
        else if (health >= 40)
        {
            conditionStr = okString.GetLocalizedString();
            conditionColor = okColor;
        }
        else if (health >= 20)
        {
            conditionStr = badString.GetLocalizedString();
            conditionColor = badColor;
        }
        else
        {
            conditionStr = severeString.GetLocalizedString();
            conditionColor = severeColor;
        }

        // Apply color only to the status tier using rich text
        string coloredConditionStr = "<color=#" + ColorUtility.ToHtmlStringRGB(conditionColor) + ">" + conditionStr + "</color>";
        conditionTxt.text = conditionString.GetLocalizedString() + " " + coloredConditionStr;

        int stability = Player.instance.playerStability.stability;
        string stabilityStr = string.Empty;
        Color stabilityColor = Color.white;
        
        if (stability >= 100)
        {
            stabilityStr = stableString.GetLocalizedString();
            stabilityColor = stableColor;
        }
        else if (stability >= 80)
        {
            stabilityStr = unsteadyString.GetLocalizedString();
            stabilityColor = unsteadyColor;
        }
        else if (stability >= 60)
        {
            stabilityStr = shakenString.GetLocalizedString();
            stabilityColor = shakenColor;
        }
        else if (stability >= 40)
        {
            stabilityStr = disturbedString.GetLocalizedString();
            stabilityColor = disturbedColor;
        }
        else
        {
            stabilityStr = fracturedString.GetLocalizedString();
            stabilityColor = fracturedColor;
        }

        // Apply color only to the status tier using rich text
        string coloredStabilityStr = "<color=#" + ColorUtility.ToHtmlStringRGB(stabilityColor) + ">" + stabilityStr + "</color>";
        stabilityTxt.text = stabilityString.GetLocalizedString() + " " + coloredStabilityStr;
    }

    void UpdateInventoryItemSelection()
    {
        if (inventoryItems != null && inventoryItems.Count > 0)
        {
            selectLeftButton.gameObject.SetActive(true);
            selectRightButton.gameObject.SetActive(true);

            //Show Description UI
            Inventory.ItemStack itemStack = inventoryItems[selectionIndex];
            inventoryDescriptionUI.ShowItemDescription(itemStack.item, itemStack.quantity);
        }
        else
        {
            //Show Description UI
            inventoryDescriptionUI.ShowItemDescription(null, 0);

            selectLeftButton.gameObject.SetActive(false);
            selectRightButton.gameObject.SetActive(false);
        }
    }

    public void SelectNextItem(int direction)
    {
        if (currentCategory == InventoryCategory.Items)
            inventoryItems = inventory.GetItems();
        else if (currentCategory == InventoryCategory.Notes)
            inventoryItems = inventory.GetNotes();

        selectionIndex += direction;
        if (selectionIndex < 0) selectionIndex = inventoryItems.Count - 1;
        if (selectionIndex >= inventoryItems.Count) selectionIndex = 0;

        UpdateInventoryItemSelection();
    }

    public void SelectCategory(int selectIndex)
    {
        if ((int)currentCategory != selectIndex)
        {
            //Update Category Selection
            currentCategory = (InventoryCategory)selectIndex;

            //Enable Selected Obj
            for (int i = 0; i < categorySelectionObjs.Length; i++)
            {
                if (i == (int)currentCategory)
                    categorySelectionObjs[i].SetActive(true);
                else
                    categorySelectionObjs[i].SetActive(false);
            }

            if (currentCategory == InventoryCategory.Items)
            {
                inventoryItems = inventory.GetItems();
                selectionIndex = Mathf.Clamp(selectionIndex, 0, inventoryItems.Count - 1);
                UpdateInventoryItemSelection();
            }
            else if (currentCategory == InventoryCategory.Notes)
            {
                inventoryItems = inventory.GetNotes();
                selectionIndex = Mathf.Clamp(selectionIndex, 0, inventoryItems.Count - 1);
                UpdateInventoryItemSelection();
            }
        }
    }
}
