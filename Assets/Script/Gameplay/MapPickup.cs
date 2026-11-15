using System.Collections;
using UnityEngine;

public class MapPickup : Interactable
{
    [SerializeField] AudioClip pickupSound;
    [Tooltip("If true, unlocks Map 2 (A/D navigation). If false, unlocks Map 1 only.")]
    [SerializeField] bool isSecondMap = false;
    [Tooltip("Optional: said a moment after the map opens (Khalid reading it).")]
    [SerializeField] SubtitleTrigger afterOpenSubtitle;
    [SerializeField] float subtitleDelay = 2f;
    [Tooltip("Optional: the map also goes into the Notes tab of the inventory (an Item of type Note with a model in the inspection lists).")]
    [SerializeField] Item notesEntry;
    [Tooltip("Optional: 'Press [M] to open Map' once the map has been closed again (like after the candy wrapper).")]
    [SerializeField] MapHintPrompt mapHint;
    AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public override void Interacted()
    {
        base.Interacted();
        StartCoroutine(Co_PickupMap());
    }

    IEnumerator Co_PickupMap()
    {
        // Play pickup sound
        if (pickupSound != null && audioSource != null)
            audioSource.PlayOneShot(pickupSound);
        else if (UISoundManager.instance != null)
            UISoundManager.instance.PlayButtonHoverSound();

        // Mark as picked up for save system
        SaveablePickup saveablePickup = GetComponent<SaveablePickup>();
        if (saveablePickup != null) saveablePickup.MarkAsPickedUp();

        // Hide object immediately - disable collider and all renderers
        Collider coll = GetComponent<Collider>();
        if (coll != null) coll.enabled = false;
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = false;

        // Small delay before fade
        yield return new WaitForSeconds(0.2f);

        // Unlock the correct map
        if (isSecondMap)
            MapHandler.instance.UnlockMap2();
        else
            MapHandler.instance.UnlockMap1();

        // ... and into the Notes, where it can be found again
        if (notesEntry != null && Player.instance != null && Player.instance.inventory != null)
            Player.instance.inventory.AddItem(notesEntry, 1);

        // Open map with fade, on the correct page
        MapHandler.instance.EnableMapMenu();
        if (isSecondMap)
            MapHandler.instance.GoToPage(1);

        if (afterOpenSubtitle != null)
        {
            yield return new WaitForSecondsRealtime(subtitleDelay);
            afterOpenSubtitle.TriggerSubtitle();
            yield return new WaitForSecondsRealtime(0.5f);
            while (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy())
                yield return null;
        }

        // how to open it again: shown once the player has closed it (the hint waits for that itself)
        if (mapHint != null) mapHint.ShowHint();

        // Destroy after map opens
        yield return new WaitForSeconds(1.5f);
        Destroy(gameObject);
    }
}
