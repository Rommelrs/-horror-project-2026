using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Interactable for broken bridges that require wooden planks to cross
/// Similar to ItemBarrierInteractable but specifically for bridge repair
/// </summary>
public class BridgePlankInteractable : Interactable
{
    public static BridgePlankInteractable currentInRange;
    
    [Header("Required Item")]
    [SerializeField] private Item requiredWoodenPlankItem;
    [SerializeField] private bool consumeItem = true;
    [SerializeField] private int requiredPlankCount = 1;

    [Header("Player Position")]
    [SerializeField] private Transform plankPlacementPosition; // Where player stands while placing plank
    
    [Header("Visuals")]
    [SerializeField] private GameObject brokenBridgeVisual;
    [SerializeField] private GameObject repairedBridgeVisual;
    [SerializeField] private GameObject[] plankObjectsToActivate; // Activate planks one by one as placed
    [SerializeField] private float placementDuration = 2f;

    [Header("Audio")]
    [SerializeField] private AudioClip plankPlaceSound;
    [SerializeField] private AudioClip noItemClip;

    [Header("Subtitles")]
    [SerializeField] private SubtitleTrigger noItemSubtitle;
    [SerializeField] private SubtitleTrigger successSubtitle;
    [SerializeField] private SubtitleTrigger progressSubtitle; // Shows "X/Y planks placed"

    [Header("Events")]
    [SerializeField] private UnityEvent onBridgeRepaired;

    private bool isRepaired = false;
    private int planksPlaced = 0;
    private AudioSource audioSource;
    private SaveableInteractable saveableInteractable;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        saveableInteractable = GetComponent<SaveableInteractable>();
        
        // Check if already repaired in a previous save
        if (saveableInteractable != null && saveableInteractable.WasAlreadyUsed())
        {
            RestoreRepairedState();
        }
    }

    private void RestoreRepairedState()
    {
        isRepaired = true;
        planksPlaced = requiredPlankCount;
        
        // Hide broken bridge
        if (brokenBridgeVisual != null)
            brokenBridgeVisual.SetActive(false);
        
        // Show repaired bridge
        if (repairedBridgeVisual != null)
            repairedBridgeVisual.SetActive(true);
        
        // Activate all plank visuals
        if (plankObjectsToActivate != null)
        {
            foreach (var plankObj in plankObjectsToActivate)
            {
                if (plankObj != null)
                    plankObj.SetActive(true);
            }
        }
        
        // Disable collider
        Collider coll = GetComponent<Collider>();
        if (coll != null)
            coll.enabled = false;
    }

    public override void Interacted()
    {
        base.Interacted();

        if (isRepaired) return;

        // Show subtitle telling player to use wooden plank from inventory
        if (noItemSubtitle != null)
            noItemSubtitle.TriggerSubtitle();
    }

    IEnumerator Co_PlacePlank()
    {
        // Fade to black
        if (FadeScreenUI.instance != null)
            FadeScreenUI.instance.FadeOut();
        
        yield return new WaitForSeconds(1f);
        
        // Move player to placement position
        if (plankPlacementPosition != null && Player.instance != null)
        {
            if (Player.instance.controller != null)
                Player.instance.controller.enabled = false;
            
            Player.instance.transform.position = plankPlacementPosition.position;
            Player.instance.transform.rotation = plankPlacementPosition.rotation;
            
            if (Player.instance.controller != null)
                Player.instance.controller.enabled = true;
        }
        
        // Play place sound
        if (audioSource != null && plankPlaceSound != null)
            audioSource.PlayOneShot(plankPlaceSound);

        // Remove plank from inventory
        if (consumeItem && requiredWoodenPlankItem != null)
            Player.instance.inventory.RemoveItem(requiredWoodenPlankItem);

        planksPlaced++;

        // Activate corresponding plank visual
        if (plankObjectsToActivate != null && planksPlaced <= plankObjectsToActivate.Length)
        {
            int index = planksPlaced - 1;
            if (plankObjectsToActivate[index] != null)
                plankObjectsToActivate[index].SetActive(true);
        }

        // Wait during placement animation
        yield return new WaitForSeconds(placementDuration);

        // Fade back in
        if (FadeScreenUI.instance != null)
            FadeScreenUI.instance.FadeIn();
        
        // Check if bridge is fully repaired
        if (planksPlaced >= requiredPlankCount)
        {
            isRepaired = true;
            
            // Mark as used in save system
            if (saveableInteractable != null)
                saveableInteractable.MarkAsUsed();

            // Hide broken bridge
            if (brokenBridgeVisual != null)
                brokenBridgeVisual.SetActive(false);

            // Show repaired bridge
            if (repairedBridgeVisual != null)
                repairedBridgeVisual.SetActive(true);

            // Trigger success subtitle
            if (successSubtitle != null)
                successSubtitle.TriggerSubtitle();

            // Disable interaction
            Collider coll = GetComponent<Collider>();
            if (coll != null)
                coll.enabled = false;

            onBridgeRepaired?.Invoke();
            
            // Clear currentInRange
            if (currentInRange == this)
                currentInRange = null;
        }
        else
        {
            // Show progress subtitle (e.g., "2/3 planks placed")
            if (progressSubtitle != null)
                progressSubtitle.TriggerSubtitle();
        }
    }
    
    public override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);
        
        if (other.CompareTag("Player") && !isRepaired)
            currentInRange = this;
    }
    
    public override void OnTriggerExit(Collider other)
    {
        base.OnTriggerExit(other);
        
        if (other.CompareTag("Player") && currentInRange == this)
            currentInRange = null;
    }
    
    public bool CanUsePlank()
    {
        return !isRepaired && requiredWoodenPlankItem != null;
    }
    
    public void UsePlankFromInventory()
    {
        if (isRepaired) return;
        
        StartCoroutine(Co_PlacePlank());
    }
    
    public int GetPlanksPlaced()
    {
        return planksPlaced;
    }
    
    public int GetRequiredPlankCount()
    {
        return requiredPlankCount;
    }
}
