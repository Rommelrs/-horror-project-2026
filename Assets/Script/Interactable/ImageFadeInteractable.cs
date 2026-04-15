using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// An interactable that fades to black and displays an image panel - press E to open, E again to close
/// Works exactly like CashRegisterInteractable
/// </summary>
public class ImageFadeInteractable : Interactable
{
    public static ImageFadeInteractable currentActive;
    public static ImageFadeInteractable currentInRange;

    [Header("UI")]
    [SerializeField] private GameObject imagePanel;
    [SerializeField] private MenuPanelSwitcher menuPanelSwitcher;
    [SerializeField] private Image panelImage;

    [Header("UV Light")]
    [SerializeField] private bool supportsUVLight = false;
    [SerializeField] private Sprite uvLightSprite;
    [SerializeField] private SubtitleTrigger uvSubtitleTrigger;

    [Header("Subtitle")]
    [SerializeField] private SubtitleTrigger subtitleTrigger;

    [Header("Activate Object On Close")]
    [SerializeField] private GameObject objectToActivate;
    [SerializeField] private bool activateOnClose = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onImageOpened;
    [SerializeField] private UnityEvent onImageClosed;
    [SerializeField] private UnityEvent onUVLightUsed;

    private bool isOpen = false;
    private bool uvLightUsed = false;
    private Sprite originalSprite;
    private Collider coll;
    private Coroutine activatingCoroutine;

    private void Awake()
    {
        coll = GetComponent<Collider>();
        
        // Store original sprite
        if (panelImage != null)
            originalSprite = panelImage.sprite;
    }

    private void Update()
    {
        if (!isOpen) return;

        // Don't close while subtitle is playing
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy())
            return;

        // Check for E key press to close
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            CloseImage();
        }
    }

    public override void Interacted()
    {
        base.Interacted();

        if (isOpen) return;

        // Disable all panels first
        menuPanelSwitcher.DisableAllPanels();

        if (activatingCoroutine != null) StopCoroutine(activatingCoroutine);
        activatingCoroutine = StartCoroutine(Co_OpenImage());

        // Pause the game
        GameManager.IsPaused = true;

        // Disable interaction collider
        if (coll != null) coll.enabled = false;
    }

    private IEnumerator Co_OpenImage()
    {
        // Fade to black
        FadeScreenUI.instance.FadeOut();

        yield return new WaitForSecondsRealtime(1.25f);

        // Show the image panel
        menuPanelSwitcher.SwitchPanel(imagePanel);

        yield return new WaitForEndOfFrame();

        // Fade back in to reveal the image
        FadeScreenUI.instance.FadeIn();

        yield return new WaitForSecondsRealtime(1f);

        isOpen = true;
        currentActive = this;

        // Trigger subtitle
        if (uvLightUsed && uvSubtitleTrigger != null)
            uvSubtitleTrigger.TriggerSubtitle();
        else if (subtitleTrigger != null)
            subtitleTrigger.TriggerSubtitle();

        onImageOpened?.Invoke();
    }

    public bool SupportsUVLight()
    {
        return supportsUVLight && !uvLightUsed;
    }

    public override void OnTriggerEnter(Collider other)
    {
        Debug.Log("OnTriggerEnter called by: " + other.gameObject.name + " | supportsUVLight: " + supportsUVLight);
        base.OnTriggerEnter(other);
        
        if (other.CompareTag("Player") && supportsUVLight)
        {
            currentInRange = this;
            Debug.Log("Player entered UV Light range: " + gameObject.name);
        }
    }

    public override void OnTriggerExit(Collider other)
    {
        base.OnTriggerExit(other);
        
        if (other.CompareTag("Player") && currentInRange == this)
            currentInRange = null;
    }

    public void UseUVLight()
    {
        if (!supportsUVLight || uvLightUsed) return;

        uvLightUsed = true;

        // Swap to UV sprite
        if (panelImage != null && uvLightSprite != null)
            panelImage.sprite = uvLightSprite;

        // Disable all panels first
        menuPanelSwitcher.DisableAllPanels();

        // Show the image panel with fade
        if (activatingCoroutine != null) StopCoroutine(activatingCoroutine);
        activatingCoroutine = StartCoroutine(Co_OpenImage());

        // Pause the game
        GameManager.IsPaused = true;

        // Disable interaction collider
        if (coll != null) coll.enabled = false;

        onUVLightUsed?.Invoke();
    }

    public void CloseImage()
    {
        isOpen = false;
        currentActive = null;

        if (activatingCoroutine != null)
        {
            StopCoroutine(activatingCoroutine);
            FadeScreenUI.instance.FadeIn();
        }

        // Hide the image panel
        imagePanel.SetActive(false);

        // Activate object
        if (activateOnClose && objectToActivate != null)
        {
            objectToActivate.SetActive(true);
        }

        // Resume the game
        GameManager.IsPaused = false;

        // Re-enable interaction collider
        if (coll != null) coll.enabled = true;

        onImageClosed?.Invoke();
    }

}
