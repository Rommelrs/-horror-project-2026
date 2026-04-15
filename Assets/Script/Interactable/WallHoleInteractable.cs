using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class WallHoleInteractable : Interactable
{
    public static WallHoleInteractable currentInRange;

    [Header("Connected Door")]
    [SerializeField] private DoorInteractable connectedDoor;

    [Header("Visuals")]
    [SerializeField] private GameObject holeVisual;
    [SerializeField] private GameObject tapeVisual;

    [Header("Audio")]
    [SerializeField] private AudioSource whisperAudioSource;
    [SerializeField] private AudioClip tapeApplyClip;

    [Header("Subtitle")]
    [SerializeField] private SubtitleTrigger subtitleTrigger;
    [SerializeField] private SubtitleTrigger repeatSubtitleTrigger;
    [SerializeField] private SubtitleTrigger tapeAppliedSubtitleTrigger;

    [Header("Events")]
    [SerializeField] private UnityEvent onTapeApplied;

    private bool isTaped = false;
    private bool hasInteracted = false;
    private AudioSource audioSource;
    private SaveableInteractable saveableInteractable;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        saveableInteractable = GetComponent<SaveableInteractable>();
        
        // Check if already taped in a previous save
        if (saveableInteractable != null && saveableInteractable.WasAlreadyUsed())
        {
            RestoreTapedState();
        }
        else
        {
            // Make sure tape visual is hidden at start
            if (tapeVisual != null)
                tapeVisual.SetActive(false);
        }
    }
    
    private void RestoreTapedState()
    {
        isTaped = true;
        
        // Stop whisper sound
        if (whisperAudioSource != null)
            whisperAudioSource.Stop();
        
        // Swap visuals
        if (holeVisual != null)
            holeVisual.SetActive(false);
        if (tapeVisual != null)
            tapeVisual.SetActive(true);
        
        // Unlock the door
        if (connectedDoor != null)
            connectedDoor.hasDoorLock = false;
        
        // Disable collider
        Collider coll = GetComponent<Collider>();
        if (coll != null)
            coll.enabled = false;
    }

    public override void Interacted()
    {
        base.Interacted();

        if (isTaped) return;

        // Trigger subtitle when interacting with the hole
        if (!hasInteracted)
        {
            hasInteracted = true;
            if (subtitleTrigger != null)
                subtitleTrigger.TriggerSubtitle();
        }
        else
        {
            if (repeatSubtitleTrigger != null)
                repeatSubtitleTrigger.TriggerSubtitle();
        }
    }

    public override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);

        Debug.Log("WallHole OnTriggerEnter: " + other.name + " | Tag: " + other.tag + " | isTaped: " + isTaped);

        if (other.CompareTag("Player") && !isTaped)
        {
            currentInRange = this;
            Debug.Log("WallHole: Player in range, currentInRange set");
        }
    }

    public override void OnTriggerExit(Collider other)
    {
        base.OnTriggerExit(other);

        if (other.CompareTag("Player") && currentInRange == this)
            currentInRange = null;
    }

    public bool CanApplyTape()
    {
        return !isTaped;
    }

    public void ApplyTape()
    {
        if (isTaped) return;

        isTaped = true;
        currentInRange = null;
        
        // Mark as used in save system
        if (saveableInteractable != null)
            saveableInteractable.MarkAsUsed();

        StartCoroutine(Co_ApplyTape());
    }

    private IEnumerator Co_ApplyTape()
    {
        // Fade to black
        FadeScreenUI.instance.FadeOut();

        yield return new WaitForSecondsRealtime(1.25f);

        // Play tape sound
        if (audioSource != null && tapeApplyClip != null)
            audioSource.PlayOneShot(tapeApplyClip);

        // Stop whisper sound
        if (whisperAudioSource != null)
            whisperAudioSource.Stop();

        // Swap visuals
        if (holeVisual != null)
            holeVisual.SetActive(false);
        if (tapeVisual != null)
            tapeVisual.SetActive(true);

        // Unlock the door
        if (connectedDoor != null)
            connectedDoor.hasDoorLock = false;

        // Fade back in
        FadeScreenUI.instance.FadeIn();

        yield return new WaitForSecondsRealtime(1f);

        // Resume the game
        GameManager.IsPaused = false;

        // Trigger subtitle
        if (tapeAppliedSubtitleTrigger != null)
            tapeAppliedSubtitleTrigger.TriggerSubtitle();

        // Disable further interaction
        Collider coll = GetComponent<Collider>();
        if (coll != null)
            coll.enabled = false;

        onTapeApplied?.Invoke();
    }
}
