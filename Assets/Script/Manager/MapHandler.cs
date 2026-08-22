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

    [System.Serializable]
    public class MapPage
    {
        public GameObject mapPanel;
        public Camera mapCamera;
        public GameObject mapCharacter;
    }

    [SerializeField] InputActionReference mapInput;
    [SerializeField] GameObject mapMenu;
    [SerializeField] CanvasGroup mapCanvasGroup;
    [SerializeField] MenuPanelSwitcher menuPanelSwitcher;
    [SerializeField] Camera mapCamera;
    [SerializeField] AudioClip mapOpeningClip;
    [SerializeField] AudioClip mapPageSwitchClip;
    [SerializeField] float mapPageSwitchFadeDuration = 0.4f;

    [Header("Additional Maps")]
    [Tooltip("Add extra map pages here. Each needs its own panel (UI) and camera.")]
    [SerializeField] MapPage[] additionalMaps;
    [Tooltip("The navigation buttons panel (Next/Prev). Hide it if only one map page.")]
    [SerializeField] GameObject pageNavigationPanel;
    [Tooltip("The original MapCharacter icon. It is shown only on the original map page.")]
    [SerializeField] GameObject mapCharacter;
    [Tooltip("The original map panel (MapMiddleGroup). Hidden when on other pages.")]
    [SerializeField] GameObject originalMapPanel;

    int currentPage = 0;

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

    private void Update()
    {
        if (mapMenu != null && mapMenu.activeSelf)
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) || UnityEngine.Input.GetKeyDown(KeyCode.D))
                NextPage();
            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.A))
                PreviousPage();
        }
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
                if (!isClosingMap)
                    StartCoroutine(Co_CloseMapWithFade());
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


    bool isSwitchingPage = false;
    bool isClosingMap = false;

    IEnumerator Co_CloseMapWithFade()
    {
        isClosingMap = true;
        FadeScreenUI.instance.FadeOut();
        yield return new WaitForSecondsRealtime(mapPageSwitchFadeDuration);
        DisableMapMenu();
        FadeScreenUI.instance.FadeIn();
        isClosingMap = false;
    }

    public void GoToPage(int pageIndex)
    {
        if (isSwitchingPage) return;
        currentPage = Mathf.Clamp(pageIndex, 0, 1 + (additionalMaps != null ? additionalMaps.Length - 1 : 0));
        ApplyCurrentPage();
    }

    public void NextPage()
    {
        if (additionalMaps == null || additionalMaps.Length == 0) return;
        if (isSwitchingPage) return;
        int totalPages = 1 + additionalMaps.Length;
        int nextPage = (currentPage + 1) % totalPages;
        StartCoroutine(Co_SwitchPage(nextPage));
    }

    public void PreviousPage()
    {
        if (additionalMaps == null || additionalMaps.Length == 0) return;
        if (isSwitchingPage) return;
        int totalPages = 1 + additionalMaps.Length;
        int prevPage = (currentPage - 1 + totalPages) % totalPages;
        StartCoroutine(Co_SwitchPage(prevPage));
    }

    IEnumerator Co_SwitchPage(int targetPage)
    {
        isSwitchingPage = true;

        // Fade to black
        FadeScreenUI.instance.FadeOut();
        yield return new WaitForSecondsRealtime(mapPageSwitchFadeDuration);

        // Switch page while screen is black
        currentPage = targetPage;
        ApplyCurrentPage();

        // Play sound
        if (mapPageSwitchClip != null && audioSource != null)
            audioSource.PlayOneShot(mapPageSwitchClip);

        // Fade back in
        FadeScreenUI.instance.FadeIn();
        yield return new WaitForSecondsRealtime(mapPageSwitchFadeDuration);

        isSwitchingPage = false;
    }

    void ApplyCurrentPage()
    {
        bool onOriginal = currentPage == 0;
        mapCamera.gameObject.SetActive(onOriginal);

        if (mapCharacter != null)
            mapCharacter.SetActive(onOriginal);
        if (originalMapPanel != null)
            originalMapPanel.SetActive(onOriginal);

        if (additionalMaps != null)
        {
            for (int i = 0; i < additionalMaps.Length; i++)
            {
                bool shouldBeActive = (i == currentPage - 1);
                if (additionalMaps[i].mapPanel != null)
                    additionalMaps[i].mapPanel.SetActive(shouldBeActive);
                if (additionalMaps[i].mapCamera != null)
                    additionalMaps[i].mapCamera.gameObject.SetActive(shouldBeActive);
                if (additionalMaps[i].mapCharacter != null)
                    additionalMaps[i].mapCharacter.SetActive(shouldBeActive);
            }
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

        // Reset to first page on open
        currentPage = 0;
        ApplyCurrentPage();

        // Show navigation panel only if there are additional maps
        if (pageNavigationPanel != null)
            pageNavigationPanel.SetActive(additionalMaps != null && additionalMaps.Length > 0);

        // Unlock cursor so player can click navigation buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

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
        // Reset any active map zoom
        MapLocationReveal reveal = FindObjectOfType<MapLocationReveal>();
        if (reveal != null) reveal.ResetZoom();

        //Disable Inventory Menu
        mapMenu.gameObject.SetActive(false);

        //Resume the Game
        GameManager.IsPaused = false;

        if (openMapCR != null)
        {
            StopCoroutine(openMapCR);
            FadeScreenUI.instance.FadeIn();
        }

        // Re-lock cursor when map closes
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        //Disable Map Camera and all additional cameras
        mapCamera.gameObject.SetActive(false);
        if (additionalMaps != null)
        {
            foreach (var page in additionalMaps)
            {
                if (page.mapPanel != null) page.mapPanel.SetActive(false);
                if (page.mapCamera != null) page.mapCamera.gameObject.SetActive(false);
            }
        }

        mapCanvasGroup.alpha = 0f;
    }
}
