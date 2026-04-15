using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CombinationLockInteractable : Interactable
{
    [Header("UI")]
    [SerializeField] private GameObject lockPanel;
    [SerializeField] private MenuPanelSwitcher menuPanelSwitcher;

    [Header("Dials")]
    [SerializeField] private Image dial0Image;
    [SerializeField] private Image dial1Image;
    [SerializeField] private Image dial2Image;
    [SerializeField] private Image dial3Image;
    [SerializeField] private Sprite[] dialSprites; // 0-9 sprites

    [Header("Buttons")]
    [SerializeField] private Button dial0Up;
    [SerializeField] private Button dial0Down;
    [SerializeField] private Button dial1Up;
    [SerializeField] private Button dial1Down;
    [SerializeField] private Button dial2Up;
    [SerializeField] private Button dial2Down;
    [SerializeField] private Button dial3Up;
    [SerializeField] private Button dial3Down;

    [Header("Code")]
    [SerializeField] private int correctCode0 = 0;
    [SerializeField] private int correctCode1 = 0;
    [SerializeField] private int correctCode2 = 0;
    [SerializeField] private int correctCode3 = 0;

    [Header("Audio")]
    [SerializeField] private AudioClip[] dialClickClips;
    [SerializeField] private AudioClip unlockClip;

    [Header("Subtitle")]
    [SerializeField] private SubtitleTrigger subtitleTrigger;
    [SerializeField] private SubtitleTrigger unlockSubtitleTrigger;

    [Header("Lock Prop")]
    [SerializeField] private Rigidbody lockPropRigidbody;

    [Header("Events")]
    [SerializeField] private UnityEvent onLockOpened;
    [SerializeField] private UnityEvent onUnlocked;

    private int currentDial0 = 0;
    private int currentDial1 = 0;
    private int currentDial2 = 0;
    private int currentDial3 = 0;

    private bool isOpen = false;
    private bool isUnlocked = false;
    private Collider coll;
    private AudioSource audioSource;
    private Coroutine activatingCoroutine;
    private SaveableInteractable saveableInteractable;

    private void Awake()
    {
        coll = GetComponent<Collider>();
        audioSource = GetComponent<AudioSource>();
        saveableInteractable = GetComponent<SaveableInteractable>();
    }

    private void Start()
    {
        // Setup button listeners
        dial0Up.onClick.AddListener(() => ChangeDial(0, 1));
        dial0Down.onClick.AddListener(() => ChangeDial(0, -1));
        dial1Up.onClick.AddListener(() => ChangeDial(1, 1));
        dial1Down.onClick.AddListener(() => ChangeDial(1, -1));
        dial2Up.onClick.AddListener(() => ChangeDial(2, 1));
        dial2Down.onClick.AddListener(() => ChangeDial(2, -1));
        dial3Up.onClick.AddListener(() => ChangeDial(3, 1));
        dial3Down.onClick.AddListener(() => ChangeDial(3, -1));

        // Initialize dial visuals
        UpdateDialVisuals();
        
        // Check if already unlocked in a previous save
        if (saveableInteractable != null && saveableInteractable.WasAlreadyUsed())
        {
            RestoreUnlockedState();
        }
    }
    
    private void RestoreUnlockedState()
    {
        isUnlocked = true;
        coll.enabled = false;
        
        // Unfreeze lock prop rigidbody
        if (lockPropRigidbody != null)
        {
            lockPropRigidbody.constraints = RigidbodyConstraints.None;
        }
    }

    private void Update()
    {
        if (!isOpen) return;

        // Check for E key press to close
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            CloseLock();
        }
    }

    public override void Interacted()
    {
        base.Interacted();

        if (isOpen || isUnlocked) return;

        // Disable all panels first
        menuPanelSwitcher.DisableAllPanels();

        if (activatingCoroutine != null) StopCoroutine(activatingCoroutine);
        activatingCoroutine = StartCoroutine(Co_OpenLock());

        // Pause the game
        GameManager.IsPaused = true;

        // Disable interaction collider
        if (coll != null) coll.enabled = false;
    }

    private IEnumerator Co_OpenLock()
    {
        // Fade to black
        FadeScreenUI.instance.FadeOut();

        yield return new WaitForSecondsRealtime(1.25f);

        // Show the lock panel
        menuPanelSwitcher.SwitchPanel(lockPanel);

        yield return new WaitForEndOfFrame();

        // Fade back in
        FadeScreenUI.instance.FadeIn();

        yield return new WaitForSecondsRealtime(1f);

        isOpen = true;

        // Trigger subtitle
        if (subtitleTrigger != null)
            subtitleTrigger.TriggerSubtitle();

        onLockOpened?.Invoke();
    }

    private void ChangeDial(int dialIndex, int direction)
    {
        if (isUnlocked) return;

        // Play click sound
        if (audioSource != null && dialClickClips != null && dialClickClips.Length > 0)
            audioSource.PlayOneShot(dialClickClips[Random.Range(0, dialClickClips.Length)]);

        // Change the dial value
        switch (dialIndex)
        {
            case 0:
                currentDial0 = (currentDial0 + direction + 10) % 10;
                break;
            case 1:
                currentDial1 = (currentDial1 + direction + 10) % 10;
                break;
            case 2:
                currentDial2 = (currentDial2 + direction + 10) % 10;
                break;
            case 3:
                currentDial3 = (currentDial3 + direction + 10) % 10;
                break;
        }

        UpdateDialVisuals();
        CheckCode();
    }

    private void UpdateDialVisuals()
    {
        if (dialSprites == null || dialSprites.Length < 10) return;

        dial0Image.sprite = dialSprites[currentDial0];
        dial1Image.sprite = dialSprites[currentDial1];
        dial2Image.sprite = dialSprites[currentDial2];
        dial3Image.sprite = dialSprites[currentDial3];
    }

    private void CheckCode()
    {
        if (currentDial0 == correctCode0 && 
            currentDial1 == correctCode1 && 
            currentDial2 == correctCode2 &&
            currentDial3 == correctCode3)
        {
            StartCoroutine(Co_Unlocked());
        }
    }

    private IEnumerator Co_Unlocked()
    {
        isUnlocked = true;
        
        // Mark as used in save system
        if (saveableInteractable != null)
            saveableInteractable.MarkAsUsed();

        // Wait for dial sound to finish
        yield return new WaitForSecondsRealtime(0.3f);

        // Play unlock sound
        if (audioSource != null && unlockClip != null)
            audioSource.PlayOneShot(unlockClip);

        yield return new WaitForSecondsRealtime(0.5f);

        // Trigger unlock subtitle
        if (unlockSubtitleTrigger != null)
            unlockSubtitleTrigger.TriggerSubtitle();

        // Unfreeze lock prop rigidbody so it falls
        if (lockPropRigidbody != null)
        {
            lockPropRigidbody.constraints = RigidbodyConstraints.None;
        }

        onUnlocked?.Invoke();
    }

    public void CloseLock()
    {
        isOpen = false;

        if (activatingCoroutine != null)
        {
            StopCoroutine(activatingCoroutine);
            FadeScreenUI.instance.FadeIn();
        }

        // Hide the lock panel
        lockPanel.SetActive(false);

        // Resume the game
        GameManager.IsPaused = false;

        // Re-enable interaction collider (only if not unlocked)
        if (coll != null && !isUnlocked)
            coll.enabled = true;

        onLockOpened?.Invoke();
    }
}
