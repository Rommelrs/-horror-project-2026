using System.Collections;
using UnityEngine;

public class MapPickup : Interactable
{
    [SerializeField] AudioClip pickupSound;
    [Tooltip("If true, unlocks Map 2 (A/D navigation). If false, unlocks Map 1 only.")]
    [SerializeField] bool isSecondMap = false;
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

        // Open map with fade, on the correct page
        MapHandler.instance.EnableMapMenu();
        if (isSecondMap)
            MapHandler.instance.GoToPage(1);

        // Destroy after map opens
        yield return new WaitForSeconds(1.5f);
        Destroy(gameObject);
    }
}
