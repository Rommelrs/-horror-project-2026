using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.EventSystems;
using System.Runtime.CompilerServices;

public class PauseHandler : MonoBehaviour
{
    public static PauseHandler instance;

    [SerializeField] InputActionReference pauseInput;
    [SerializeField] MenuPanelSwitcher menuPanelSwitcher;

    [SerializeField] GameObject guiPanel;
    [SerializeField] bool disableGUI = true;

    public delegate void OnResumeGame();
    public event OnResumeGame OnResumeGameEvent;
    bool isPaused;
    public bool IsPaused { get { return isPaused; } }

    private void OnEnable()
    {
        pauseInput.action.Enable();
    }

    private void OnDestroy()
    {
        //Unsubscribe to the pause input action
        pauseInput.action.Disable();
        pauseInput.action.performed -= PauseButtonPressed;
    }

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        //Subscribe to the pause input action
        pauseInput.action.performed += PauseButtonPressed;
    }

    //On Pause Input pressed
    private void PauseButtonPressed(InputAction.CallbackContext callbackContext)
    {
        if (LoadingHandler.IsLoading())
            return;

        //Subtitle is showing so ignore pause input
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy())
            return;

        //If Inventory is open disable inventory
        if (InventoryUI.instance.InventoryIsActive())
        {
            InventoryUI.instance.DisableInventoryMenu();
            return;
        }

        //If Map is opened disable Map Menu
        if (MapHandler.instance.MapIsActive())
        {
            MapHandler.instance.DisableMapMenu();
            return;
        }

        //If Eye Peak is activated then Exit out of it
        if (EyePeakHandler.instance && EyePeakHandler.instance.IsEyePeakActivated())
        {
            EyePeakHandler.instance.ExitPeakMode();
            return;
        }

        //If Item Inspection Menu is active & is reading note
        if (ItemInspectionHandler.instance && ItemInspectionHandler.instance.IsNoteAndReading())
        {
            ItemInspectionHandler.instance.CancelReading();
            return;
        }

        //If Item Inspection Menu is active & is opened through inventory then open inventory
        if (ItemInspectionHandler.instance && ItemInspectionHandler.instance.InspectionMenuIsActive() && ItemInspectionHandler.instance.IsOpenedThroughInventory)
        {
            InventoryUI.instance.EnableInventoryUI(true);
            return;
        }

        //If Item Inspection Menu is active then Close it
        if (ItemInspectionHandler.instance && ItemInspectionHandler.instance.InspectionMenuIsActive())
        {
            ItemInspectionHandler.instance.CloseInspectionMenu();
            return;
        }

        //If CashRegister Menu is active then Close it
        if (CashRegisterInteractable.instance && CashRegisterInteractable.instance.CashRegisterMenuIsActive())
        {
            CashRegisterInteractable.instance.CloseCashRegisterMenu();
            return;
        }
        
        //If Save Menu is active then Close it
        SaveMenuUI saveMenu = FindObjectOfType<SaveMenuUI>();
        if (saveMenu != null && saveMenu.IsMenuActive())
        {
            saveMenu.CloseSaveMenu();
            return;
        }

        if (!isPaused)
        {
            //If not paused then pause
            PauseGame();
        }
        else
        {
            //If paused then resume
            ResumeGame();
        }
    }

    //Pause the game
    public void PauseGame()
    {
        //Check if the game is already gameover or gamewon
        if (LevelManager.instance != null && (LevelManager.instance.isGameOver || LevelManager.instance.isGameWon))
            return;

        isPaused = true;

        //Show Pause Menu
        menuPanelSwitcher.SwitchPanel(0);

        GameManager.IsPaused = true;

        if (disableGUI)
            guiPanel.SetActive(false);
    }

    //Resume the game
    public void ResumeGame()
    {
        isPaused = false;
        GameManager.IsPaused = false;

        if (disableGUI)
            guiPanel.SetActive(true);

        //Hide Pause Menu
        menuPanelSwitcher.DisableAllPanels();

        //Call Event
        OnResumeGameEvent?.Invoke();
    }
}
