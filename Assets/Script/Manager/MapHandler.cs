using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class MapHandler : MonoBehaviour
{
    public static MapHandler instance;

    [SerializeField] InputActionReference mapInput;
    [SerializeField] GameObject mapMenu;
    [SerializeField] CanvasGroup mapCanvasGroup;
    [SerializeField] MenuPanelSwitcher menuPanelSwitcher;
    [SerializeField] Camera mapCamera;
    [SerializeField] AudioClip mapOpeningClip;

    Coroutine openMapCR;
    AudioSource audioSource;

    private void Awake()
    {
        instance = this;
        audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        mapInput.action.Enable();
    }

    private void OnDisable()
    {
        mapInput.action.Disable();
    }

    private void Start()
    {
        mapInput.action.performed += OnMapButtonPressed;
        
        // If player has map, briefly open/close it to initialize markers
        if (Player.instance != null && Player.instance.hasMap)
        {
            StartCoroutine(Co_InitializeMapMarkers());
        }
    }
    
    private System.Collections.IEnumerator Co_InitializeMapMarkers()
    {
        // Wait for everything to be ready
        yield return new WaitForEndOfFrame();
        
        // Briefly enable map menu to trigger SaveableUIImage.Start()
        if (mapMenu != null)
        {
            mapMenu.SetActive(true);
            yield return null;
            mapMenu.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        mapInput.action.performed -= OnMapButtonPressed;
    }

    public bool MapIsActive()
    {
        return mapMenu.gameObject.activeSelf;
    }

    public void OnMapButtonPressed(InputAction.CallbackContext callbackContext)
    {
        if (Player.instance == null || !Player.instance.hasMap)
            return;

        if (LevelManager.instance.isGameOver || LevelManager.instance.isGameWon)
            return;

        if (callbackContext.performed)
        {
            if (mapMenu.gameObject.activeSelf)
            {
                //Disable Inventory Menu
                DisableMapMenu();
            }
            else
            {
                EnableMapMenu();
            }
        }
    }

    public void UnlockMap()
    {
        if (Player.instance != null)
        {
            Player.instance.hasMap = true;
            // Save to PlayerPrefs
            PlayerPrefs.SetInt("HasMap", 1);
            PlayerPrefs.Save();
        }
    }

    public void EnableMapMenu(bool instantEnable = false)
    {
        if (Player.instance == null || !Player.instance.hasMap)
            return;

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
        menuPanelSwitcher.SwitchPanel(mapMenu);

        //Resume the Game
        GameManager.IsPaused = true;

        //Enable Map Camera
        mapCamera.gameObject.SetActive(true);

        if (instantEnable)
        {
            if (openMapCR != null) StopCoroutine(openMapCR);

            //Show
            mapCanvasGroup.alpha = 1f;
        }
        else
        {

            //Hide
            mapCanvasGroup.alpha = 0f;

            if (openMapCR != null) StopCoroutine(openMapCR);
            openMapCR = StartCoroutine(Co_EnableMapUI());
        }
    }

    IEnumerator Co_EnableMapUI()
    {
        FadeScreenUI.instance.FadeOut();
        yield return new WaitForSecondsRealtime(0.5f);

        //Play Sfx
        if (mapOpeningClip != null && audioSource != null)
            audioSource.PlayOneShot(mapOpeningClip);

        yield return new WaitForSecondsRealtime(0.5f);

        mapCanvasGroup.alpha = 1f;
        FadeScreenUI.instance.FadeIn();
    }

    public void DisableMapMenu()
    {
        //Disable Inventory Menu
        mapMenu.gameObject.SetActive(false);

        //Resume the Game
        GameManager.IsPaused = false;

        if (openMapCR != null)
        {
            StopCoroutine(openMapCR);
            FadeScreenUI.instance.FadeIn();
        }

        //Disable Map Camera
        mapCamera.gameObject.SetActive(false);

        mapCanvasGroup.alpha = 0f;
    }
}
