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

        // Disable collider so player can't interact again
        Collider coll = GetComponent<Collider>();
        if (coll != null) coll.enabled = false;

        // Small delay before fade
        yield return new WaitForSeconds(0.2f);

        // Unlock the correct map
        if (isSecondMap)
            MapHandler.instance.UnlockMap2();
        else
            MapHandler.instance.UnlockMap1();

        // Open map with fade
        MapHandler.instance.EnableMapMenu();

        // Destroy after map opens
        yield return new WaitForSeconds(1.5f);
        Destroy(gameObject);
    }
}
