using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ItemBarrierInteractable : Interactable
{
    public static ItemBarrierInteractable currentInRange;
    
    [Header("Required Item")]
    [SerializeField] private Item requiredItem;
    [SerializeField] private bool consumeItem = true;

    [Header("Visuals")]
    [SerializeField] private GameObject barrierObject;
    [SerializeField] private GameObject objectToActivateAfter;

    [Header("Audio")]
    [SerializeField] private AudioClip interactClip;
    [SerializeField] private AudioClip noItemClip;

    [Header("Subtitles")]
    [SerializeField] private SubtitleTrigger noItemSubtitle;
    [SerializeField] private SubtitleTrigger successSubtitle;

    [Header("Events")]
    [SerializeField] private UnityEvent onBarrierCleared;

    private bool isCleared = false;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public override void Interacted()
    {
        base.Interacted();

        if (isCleared) return;

        // Check if player has the required item
        bool hasItem = false;

        if (requiredItem != null && Player.instance != null)
        {
            foreach (var stack in Player.instance.inventory.GetItems())
            {
                if (stack.item == requiredItem)
                {
                    hasItem = true;
                    break;
                }
            }
        }

        if (!hasItem)
        {
            // Player doesn't have the item
            if (audioSource != null && noItemClip != null)
                audioSource.PlayOneShot(noItemClip);

            if (noItemSubtitle != null)
                noItemSubtitle.TriggerSubtitle();

            return;
        }

        // Player has the item, clear the barrier
        StartCoroutine(Co_ClearBarrier());
    }

    IEnumerator Co_ClearBarrier()
    {
        isCleared = true;

        // Play interact sound
        if (audioSource != null && interactClip != null)
            audioSource.PlayOneShot(interactClip);

        // Remove item from inventory
        if (consumeItem && requiredItem != null)
            Player.instance.inventory.RemoveItem(requiredItem);

        yield return new WaitForSeconds(0.5f);

        // Remove barrier
        if (barrierObject != null)
            barrierObject.SetActive(false);

        // Activate new object
        if (objectToActivateAfter != null)
            objectToActivateAfter.SetActive(true);

        // Trigger subtitle
        if (successSubtitle != null)
            successSubtitle.TriggerSubtitle();

        // Disable interaction
        Collider coll = GetComponent<Collider>();
        if (coll != null)
            coll.enabled = false;

        onBarrierCleared?.Invoke();
    }
    
    public override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);
        
        if (other.CompareTag("Player") && !isCleared)
            currentInRange = this;
    }
    
    public override void OnTriggerExit(Collider other)
    {
        base.OnTriggerExit(other);
        
        if (other.CompareTag("Player") && currentInRange == this)
            currentInRange = null;
    }
    
    public bool CanUseItem(Item item)
    {
        return !isCleared && requiredItem != null && item == requiredItem;
    }
    
    public void UseItemFromInventory()
    {
        if (isCleared) return;
        
        StartCoroutine(Co_ClearBarrier());
    }
}
