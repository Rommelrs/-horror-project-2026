using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fusebox : Interactable
{
    public bool hasEnergy = false;

    [SerializeField] AudioClip noFuseClip;
    [SerializeField] AudioClip fuseAddedClip;
    [SerializeField] AudioClip energyRestoreClip;

    [SerializeField] SubtitleTrigger noFuseSubtitleTrigger;
    [SerializeField] SubtitleTrigger haveFuseSubtitleTrigger;

    [SerializeField] SubtitleTrigger energyRestoredSubtitleTrigger;

    [SerializeField] GameObject offObj;
    [SerializeField] GameObject onObj;

    [Header("Timing")]
    [SerializeField] float fadeOutDuration = 1.5f;
    [SerializeField] float energyRestoreDuration = 3.5f;
    [SerializeField] float fadeInDuration = 1f;

    AudioSource audioSource;
    Collider coll;
    SaveableInteractable saveableInteractable;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        coll = GetComponent<Collider>();
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
        hasEnergy = true;
        coll.enabled = false;
        
        // Swap visuals
        if (offObj != null)
            offObj.SetActive(false);
        if (onObj != null)
            onObj.SetActive(true);
    }

    public override void Interacted()
    {
        base.Interacted();

        if (hasEnergy)
            return;

        if (Player.instance && Player.instance.inventory.HasFuse() == false)
        {
            //Player has no fuse
            //Play SFX
            if (noFuseClip != null) audioSource.PlayOneShot(noFuseClip);

            //Trigger subtitle
            noFuseSubtitleTrigger.TriggerSubtitle();

            return;
        }

        //Trigger Have Subtitle
        haveFuseSubtitleTrigger.TriggerSubtitle();
    }

    public void UseFuse()
    {
        StartCoroutine(Co_FuseboxInteracted());
    }

    IEnumerator Co_FuseboxInteracted()
    {
        coll.enabled = false;
        hasEnergy = true;
        Player.instance.pauseMovement = true;
        
        // Mark as used in save system
        if (saveableInteractable != null)
            saveableInteractable.MarkAsUsed();

        //Remove fusebox
        Player.instance.RemoveFusebox();

        //Play SFX
        if (fuseAddedClip != null) audioSource.PlayOneShot(fuseAddedClip);

        //Fade screen to black
        FadeScreenUI.instance.FadeOut();

        yield return new WaitForSeconds(fadeOutDuration);

        //Play energy Restore SFX
        audioSource.PlayOneShot(energyRestoreClip);

        yield return new WaitForSeconds(energyRestoreDuration);

        //Fade In
        FadeScreenUI.instance.FadeIn();

        offObj.SetActive(false);
        onObj.SetActive(true);

        yield return new WaitForSeconds(fadeInDuration);

        //Trigger subtitle
        energyRestoredSubtitleTrigger.TriggerSubtitle();

        Player.instance.pauseMovement = false;
    }

    public override void OnTriggerEnter(Collider other)
    {
        //Player Enter Inspectable
        if (other.gameObject.CompareTag("Player"))
        {
            Player.instance.SetFusebox(this);

            InteractionHandler.instance.InspectableItemTriggerEnter(this);
        }
    }

    public override void OnTriggerExit(Collider other)
    {
        //Player Exit Inspectable
        if (other.gameObject.CompareTag("Player"))
        {
            Player.instance.RemoveFusebox();

            InteractionHandler.instance.InspectableItemTriggerExit(this);
        }
    }
}
