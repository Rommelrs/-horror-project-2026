using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class BurnableObstacle : MonoBehaviour
{
    public static BurnableObstacle currentInRange;
    
    [Header("Burn Settings")]
    [SerializeField] private ItemType requiredItemType = ItemType.Lighter;
    [SerializeField] private GameObject obstacleObject; // The object to burn/destroy
    [SerializeField] private GameObject[] additionalObjectsToDestroy;
    
    [Header("Player Position")]
    [SerializeField] private Transform burnPosition; // Where player stands while burning
    
    [Header("Effects")]
    [SerializeField] private GameObject fireParticleEffect;
    [SerializeField] private Transform fireSpawnPoint;
    [SerializeField] private AudioClip burnSound;
    [SerializeField] private float burnDuration = 3f;
    
    [Header("Replacement")]
    [SerializeField] private bool activateExistingObject = false; // If true, activates the object instead of instantiating
    [SerializeField] private GameObject replacementObject; // Object to spawn/activate after burning
    
    
    [Header("Subtitles")]
    [SerializeField] private SubtitleTrigger noItemSubtitle;
    [SerializeField] private SubtitleTrigger successSubtitle;
    
    [Header("Events")]
    [SerializeField] private UnityEvent onBurnComplete;
    
    private bool hasBeenBurned = false;
    private bool playerInRange = false;
    private SaveableInteractable saveableInteractable;
    
    private void Start()
    {
        saveableInteractable = GetComponent<SaveableInteractable>();
        
        // Check if already burned in a previous save
        if (saveableInteractable != null && saveableInteractable.WasAlreadyUsed())
        {
            RestoreBurnedState();
        }
    }
    
    private void RestoreBurnedState()
    {
        hasBeenBurned = true;
        
        // Destroy obstacle
        if (obstacleObject != null)
        {
            // Activate or instantiate replacement
            if (replacementObject != null)
            {
                if (activateExistingObject)
                {
                    replacementObject.SetActive(true);
                }
                else
                {
                    Instantiate(replacementObject, obstacleObject.transform.position, obstacleObject.transform.rotation);
                }
            }
            
            Destroy(obstacleObject);
        }
        
        // Destroy additional objects
        if (additionalObjectsToDestroy != null)
        {
            foreach (var obj in additionalObjectsToDestroy)
            {
                if (obj != null)
                    Destroy(obj);
            }
        }
        
        // Disable collider
        Collider coll = GetComponent<Collider>();
        if (coll != null)
            coll.enabled = false;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (!hasBeenBurned)
                currentInRange = this;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (currentInRange == this)
                currentInRange = null;
        }
    }
    
    private void Update()
    {
        if (playerInRange && !hasBeenBurned && Input.GetKeyDown(KeyCode.E))
        {
            // Don't trigger if subtitle system is already active or in cooldown
            if (SubtitleManager.instance != null && (SubtitleManager.instance.IsSubtitleBusy() || SubtitleManager.instance.IsWithinCooldownPeriod()))
                return;
                
            if (noItemSubtitle != null)
                noItemSubtitle.TriggerSubtitle();
        }
    }
    
    
    public void TryBurn()
    {
        if (hasBeenBurned)
            return;
        
        if (PlayerHasRequiredItem())
        {
            Burn();
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
    
    private void Burn()
    {
        hasBeenBurned = true;
        
        // Mark as used in save system
        if (saveableInteractable != null)
            saveableInteractable.MarkAsUsed();
        
        StartCoroutine(Co_Burn());
    }
    
    private IEnumerator Co_Burn()
    {
        // Fade to black
        if (FadeScreenUI.instance != null)
            FadeScreenUI.instance.FadeOut();
        
        yield return new WaitForSeconds(1f);
        
        // Move player to burn position
        if (burnPosition != null && Player.instance != null)
        {
            if (Player.instance.controller != null)
                Player.instance.controller.enabled = false;
            
            Player.instance.transform.position = burnPosition.position;
            Player.instance.transform.rotation = burnPosition.rotation;
            
            if (Player.instance.controller != null)
                Player.instance.controller.enabled = true;
        }
        
        // Play burn sound
        if (burnSound != null && SoundEffectManager.instance != null)
            SoundEffectManager.instance.PlaySFXAtPosition(burnSound, transform.position);
        
        // Destroy obstacle and spawn/activate replacement
        if (obstacleObject != null)
        {
            // Spawn or activate replacement object
            if (replacementObject != null)
            {
                if (activateExistingObject)
                {
                    // Activate existing GameObject
                    replacementObject.SetActive(true);
                }
                else
                {
                    // Instantiate new GameObject at obstacle's position
                    Instantiate(replacementObject, obstacleObject.transform.position, obstacleObject.transform.rotation);
                }
            }
            
            Destroy(obstacleObject);
        }
        
        // Destroy additional objects
        if (additionalObjectsToDestroy != null)
        {
            foreach (var obj in additionalObjectsToDestroy)
            {
                if (obj != null)
                    Destroy(obj);
            }
        }
        
        // Wait during "burning"
        yield return new WaitForSeconds(burnDuration);
        
        // Fade back in
        if (FadeScreenUI.instance != null)
            FadeScreenUI.instance.FadeIn();
        
        // Spawn fire effect (after fade in so player sees it)
        if (fireParticleEffect != null)
        {
            Vector3 spawnPos = fireSpawnPoint != null ? fireSpawnPoint.position : transform.position;
            GameObject fireEffect = Instantiate(fireParticleEffect, spawnPos, Quaternion.identity);
            Destroy(fireEffect, 5f);
        }
        
        // Trigger success subtitle
        if (successSubtitle != null)
            successSubtitle.TriggerSubtitle();
        
        // Trigger events
        onBurnComplete?.Invoke();
        
        // Clear currentInRange
        if (currentInRange == this)
            currentInRange = null;
    }
    
    public bool CanUseLighter()
    {
        return !hasBeenBurned;
    }
    
    public void BurnFromInventory(ItemType itemTypeBeingUsed)
    {
        if (hasBeenBurned) return;
        
        // Check if the item being used matches the required item type
        if (itemTypeBeingUsed != requiredItemType)
        {
            // Show no item subtitle
            if (noItemSubtitle != null)
                noItemSubtitle.TriggerSubtitle();
            
            return;
        }
        
        Burn();
    }
}
