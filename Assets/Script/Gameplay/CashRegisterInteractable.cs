using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.InputSystem;

public class CashRegisterInteractable : Interactable
{
    public static CashRegisterInteractable instance;

    [Header("UI")]
    [SerializeField] GameObject cashRegisterMenu;
    [SerializeField] GameObject cashRegisterDrawerPanel;
    [SerializeField] MenuPanelSwitcher menuPanelSwitcher;
    [SerializeField] TMP_Text codeEnterTxt;
    [SerializeField] int maxCodeLength = 6;
    [SerializeField] string code;
    [SerializeField] SubtitleTrigger noFuseSubtitleTrigger;
    [SerializeField] SubtitleTrigger enteredCorrectCodeSubtitleTrigger;

    [SerializeField] AudioClip typeClip;
    [SerializeField] AudioClip correctCodeClip;
    [SerializeField] AudioClip wrongCodeClip;

    [SerializeField] Fusebox fuseBox;
    [SerializeField] SubtitleTrigger mapOpenedSubtitleTrigger;

    public UnityEvent OnCashRegisterOpenedSuccessfully;

    [Header("Input Actions")]
    [SerializeField] InputActionReference numberInput;   // Any Key
    [SerializeField] InputActionReference enterInput;

    AudioSource audioSource;
    Collider coll;
    bool cashRegisterMenuActivated = false;
    bool correctCodeSubmitted = false;
    string currentCode = "";
    Coroutine activatingCashRegisterCR;
    SaveableInteractable saveableInteractable;

    private void Awake()
    {
        instance = this;
        coll = GetComponent<Collider>();
        audioSource = GetComponent<AudioSource>();
        saveableInteractable = GetComponent<SaveableInteractable>();
    }
    
    private void Start()
    {
        // Check if already used in a previous save
        if (saveableInteractable != null && saveableInteractable.WasAlreadyUsed())
        {
            RestoreUsedState();
        }
    }
    
    private void RestoreUsedState()
    {
        correctCodeSubmitted = true;
        coll.enabled = false;
        
        // Unlock map
        if (MapHandler.instance != null)
            MapHandler.instance.UnlockMap();
        
        // Show drawer panel
        if (cashRegisterDrawerPanel != null)
            cashRegisterDrawerPanel.SetActive(true);
    }

    private void OnEnable()
    {
        numberInput.action.performed += OnNumberInput;
        enterInput.action.performed += OnEnterPressed;
    }

    private void OnDisable()
    {
        numberInput.action.performed -= OnNumberInput;
        enterInput.action.performed -= OnEnterPressed;
    }

    public override void Interacted()
    {
        base.Interacted();
        ActivateCashRegisterMenu();
    }

    public void ActivateCashRegisterMenu()
    {
        cashRegisterMenuActivated = true;

        currentCode = "";
        UpdateCodeText();

        //Enable Cash Register Menu
        menuPanelSwitcher.DisableAllPanels();

        if (activatingCashRegisterCR != null) StopCoroutine(activatingCashRegisterCR);
        activatingCashRegisterCR = StartCoroutine(Co_ActivateCashRegister());

        //Pause the Game
        GameManager.IsPaused = true;

        //Disable Interaction
        coll.enabled = false;

        numberInput.action.Enable();
        enterInput.action.Enable();
    }

    IEnumerator Co_ActivateCashRegister()
    {
        //Fade screen to black
        FadeScreenUI.instance.FadeOut();

        yield return new WaitForSecondsRealtime(1.25f);

        menuPanelSwitcher.SwitchPanel(cashRegisterMenu);

        yield return new WaitForEndOfFrame();

        //Fade In
        FadeScreenUI.instance.FadeIn();

        yield return new WaitForSecondsRealtime(1f);

        cashRegisterMenuActivated = true;

        if (attemptToPlayNoPowerSubtitleCR != null) StopCoroutine(attemptToPlayNoPowerSubtitleCR);
        attemptToPlayNoPowerSubtitleCR = StartCoroutine(Co_AttemptToPlayNoPowerSubtitle());
    }

    public void CloseCashRegisterMenu()
    {
        cashRegisterMenuActivated = false;

        if (activatingCashRegisterCR != null)
        {
            StopCoroutine(activatingCashRegisterCR);

            //Fade In
            FadeScreenUI.instance.FadeIn();
        }

        //Stop Attempting to show no power subtitle
        if (attemptToPlayNoPowerSubtitleCR != null) StopCoroutine(attemptToPlayNoPowerSubtitleCR);

        //Disable Cash Register Menu
        cashRegisterMenu.SetActive(false);

        //Resume the Game
        GameManager.IsPaused = false;

        //Enable Interaction
        if(!correctCodeSubmitted)
            coll.enabled = true;

        numberInput.action.Disable();
        enterInput.action.Disable();

        currentCode = "";
        UpdateCodeText();
        
        // If code was correct, auto-open the map
        if (correctCodeSubmitted && MapHandler.instance != null)
        {
            StartCoroutine(Co_OpenMapAfterClose());
        }
    }
    
    IEnumerator Co_OpenMapAfterClose()
    {
        // Lock input immediately - no gap where M can be pressed
        MapLocationReveal.IsSequenceActive = true;
        Player.instance.pauseMovement = true;

        yield return new WaitForSecondsRealtime(1f);
        
        // Open the map
        MapHandler.instance.EnableMapMenu();

        // Wait for player to see the map before showing subtitle
        yield return new WaitForSecondsRealtime(2f);

        // Trigger subtitle
        if (mapOpenedSubtitleTrigger != null)
            mapOpenedSubtitleTrigger.TriggerSubtitle();

        // Wait for subtitle to fully finish before unlocking
        yield return new WaitForSecondsRealtime(0.5f);
        while (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy())
            yield return null;

        // Now unlock - player can interact with map normally
        MapLocationReveal.IsSequenceActive = false;
        Player.instance.pauseMovement = false;
    }

    Coroutine attemptToPlayNoPowerSubtitleCR;

    IEnumerator Co_AttemptToPlayNoPowerSubtitle()
    {
        yield return new WaitForSecondsRealtime(1.4f);

        if (fuseBox.hasEnergy == false)
        {
            //Trigger subtitle
            noFuseSubtitleTrigger.TriggerSubtitle();
        }
    }

    void OnNumberInput(InputAction.CallbackContext context)
    {
        //if (context.action.WasPerformedThisFrame())
        //{
        //    // Get the key that was pressed
        //    int inputNumber = Mathf.RoundToInt((float)context.ReadValue<float>());
        //    OnNumberButtonPressed(inputNumber);
        //}
    }

    public void OnNumberButtonPressed(int buttonIndex)
    {
        if (correctCodeSubmitted)
            return;

        if (!cashRegisterMenuActivated)
            return;

        if (!cashRegisterMenu.activeInHierarchy)
            return;

        if (currentCode.Length >= maxCodeLength)
            return;


        if (fuseBox.hasEnergy == false)
        {
            //Player has no fuse
            //Play SFX
            if (wrongCodeClip != null) audioSource.PlayOneShot(wrongCodeClip);

            return;
        }

        currentCode += buttonIndex.ToString();
        UpdateCodeText();

        //Play SFX
        if (typeClip != null) audioSource.PlayOneShot(typeClip);
    }

    void OnEnterPressed(InputAction.CallbackContext context)
    {
        //if (context.action.WasPerformedThisFrame())
        //{
        //    OnEnterButtonPressed();
        //}
    }

    public void OnEnterButtonPressed()
    {
        if (correctCodeSubmitted)
            return;

        if (!cashRegisterMenuActivated)
            return;

        if (!cashRegisterMenu.activeInHierarchy)
            return;

        if (fuseBox.hasEnergy == false)
        {
            //Player has no fuse
            //Play SFX
            if (wrongCodeClip != null) audioSource.PlayOneShot(wrongCodeClip);

            return;
        }

        SubmitCode();
    }

    void SubmitCode()
    {
        //Debug.Log("Submitted Code: " + currentCode);

        if(currentCode == code)
        {
            StartCoroutine(Co_SubmittedCorrectCode());
        }
        else
        {
            //Wrong Code
            currentCode = "";
            UpdateCodeText();

            //Play SFX
            if (wrongCodeClip != null) audioSource.PlayOneShot(wrongCodeClip);
        } 
    }

    IEnumerator Co_SubmittedCorrectCode()
    {
        //Correct Code Submitted
        correctCodeSubmitted = true;

        //Disable Further Interaction
        coll.enabled = false;
        
        // Mark as used in save system
        if (saveableInteractable != null)
            saveableInteractable.MarkAsUsed();

        //Trigger Event
        OnCashRegisterOpenedSuccessfully?.Invoke();

        //Unlock Map
        if (MapHandler.instance != null)
            MapHandler.instance.UnlockMap();

        //Play SFX
        if (correctCodeClip != null) audioSource.PlayOneShot(correctCodeClip);
      
        cashRegisterDrawerPanel.gameObject.SetActive(true);

        yield return new WaitForSecondsRealtime(0.5f);

        //Trigger Subtitle
        enteredCorrectCodeSubtitleTrigger.TriggerSubtitle();

        //yield return new WaitForSecondsRealtime(9.5f);

        ////Entered Correct Code
        //CloseCashRegisterMenu();
    }

    void UpdateCodeText()
    {
        codeEnterTxt.text = currentCode;
    }

    public bool CashRegisterMenuIsActive()
    {
        return cashRegisterMenuActivated;
    }
}
