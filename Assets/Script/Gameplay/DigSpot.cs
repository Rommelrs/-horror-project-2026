using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class DigSpot : MonoBehaviour
{
    [Header("Dig Settings")]
    [SerializeField] private ItemType requiredItemType = ItemType.Shovel;
    [SerializeField] private GameObject coverObject; // The dirt/sand lid to hide
    [SerializeField] private GameObject revealedObject; // Optional: object to show after digging
    [SerializeField] private GameObject[] objectsToDestroy; // Objects to destroy after digging (e.g., X marks the spot)
    
    [Header("Player Position")]
    [SerializeField] private Transform digPosition; // Where player stands while digging
    
    [Header("Effects")]
    [SerializeField] private GameObject digParticleEffect;
    [SerializeField] private AudioClip digSound;
    [SerializeField] private float digDuration = 2f; // How long the black screen lasts
    [SerializeField] private int stabilityDecreaseAmount = 15; // Stability lost after digging
    
    [Header("Subtitles")]
    [SerializeField] private SubtitleTrigger noItemSubtitle;
    [SerializeField] private SubtitleTrigger successSubtitle;
    
    [Header("Events")]
    [SerializeField] private UnityEvent onDigComplete;
    
    private bool hasBeenDug = false;
    private bool playerInRange = false;
    
    private void Start()
    {
        // Check save system if already dug
        SaveableInteractable saveableInteractable = GetComponent<SaveableInteractable>();
        if (saveableInteractable != null && saveableInteractable.WasAlreadyUsed())
        {
            // Already dug, restore state
            hasBeenDug = true;
            RestoreDugState();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }
    
    private void Update()
    {
        if (playerInRange && !hasBeenDug && Input.GetKeyDown(KeyCode.E))
        {
            // Don't trigger if subtitle system is already active or in cooldown
            if (SubtitleManager.instance != null && (SubtitleManager.instance.IsSubtitleBusy() || SubtitleManager.instance.IsWithinCooldownPeriod()))
                return;
                
            if (!PlayerHasRequiredItem() && noItemSubtitle != null)
                noItemSubtitle.TriggerSubtitle();
            else
                TryDig();
        }
    }
    
    public void TryDig()
    {
        if (hasBeenDug)
            return;
        
        // Check if player has shovel
        if (PlayerHasRequiredItem())
        {
            Dig();
        }
    }
    
    private bool PlayerHasRequiredItem()
    {
        if (Player.instance == null || Player.instance.inventory == null)
            return false;
        
        var items = Player.instance.inventory.GetItems();
        foreach (var itemStack in items)
        {
            if (itemStack.item != null && itemStack.item.itemType == requiredItemType)
                return true;
        }
        
        return false;
    }
    
    private void Dig()
    {
        hasBeenDug = true;
        
        // Mark as dug in save system
        SaveableInteractable saveableInteractable = GetComponent<SaveableInteractable>();
        if (saveableInteractable != null)
        {
            saveableInteractable.MarkAsUsed();
        }
        
        StartCoroutine(Co_Dig());
    }
    
    private IEnumerator Co_Dig()
    {
        // Fade to black
        if (FadeScreenUI.instance != null)
            FadeScreenUI.instance.FadeOut();
        
        yield return new WaitForSeconds(1f); // Wait for fade
        
        // Move player to dig position
        if (digPosition != null && Player.instance != null)
        {
            if (Player.instance.controller != null)
                Player.instance.controller.enabled = false;
            
            Player.instance.transform.position = digPosition.position;
            Player.instance.transform.rotation = digPosition.rotation;
            
            if (Player.instance.controller != null)
                Player.instance.controller.enabled = true;
        }
        
        // Hide the cover (dirt/sand)
        if (coverObject != null)
            coverObject.SetActive(false);
        
        // Show revealed object (buried item)
        if (revealedObject != null)
            revealedObject.SetActive(true);
        
        // Play particle effect
        if (digParticleEffect != null)
        {
            GameObject particles = Instantiate(digParticleEffect, transform.position, Quaternion.identity);
            Destroy(particles, 3f);
        }
        
        // Play sound
        if (digSound != null && SoundEffectManager.instance != null)
            SoundEffectManager.instance.PlaySFXAtPosition(digSound, transform.position);
        
        // Destroy objects (e.g., X marks the spot)
        if (objectsToDestroy != null)
        {
            foreach (var obj in objectsToDestroy)
            {
                if (obj != null)
                    Destroy(obj);
            }
        }
        
        // Wait during "digging"
        yield return new WaitForSeconds(digDuration);
        
        // Fade back in
        if (FadeScreenUI.instance != null)
            FadeScreenUI.instance.FadeIn();
        
        // Decrease player stability
        if (Player.instance != null && Player.instance.playerStability != null)
            Player.instance.playerStability.DecreaseStability(stabilityDecreaseAmount);
        
        // Trigger success subtitle
        if (successSubtitle != null)
            successSubtitle.TriggerSubtitle();
        
        // Trigger events
        onDigComplete?.Invoke();
    }
    
    /// <summary>
    /// Restore the dig spot to its dug state without animation (used by save system)
    /// </summary>
    private void RestoreDugState()
    {
        // Hide the cover (dirt/sand)
        if (coverObject != null)
            coverObject.SetActive(false);
        
        // Show revealed object (buried item)
        if (revealedObject != null)
            revealedObject.SetActive(true);
        
        // Destroy objects (e.g., X marks the spot)
        if (objectsToDestroy != null)
        {
            foreach (var obj in objectsToDestroy)
            {
                if (obj != null)
                    Destroy(obj);
            }
        }
        
        // Disable trigger so player can't interact
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;
    }
}
