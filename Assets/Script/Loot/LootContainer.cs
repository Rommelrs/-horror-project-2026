using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ToolBox.Pools;

public class LootContainer : Interactable
{
    [Header("Loot Settings")]
    [Tooltip("The loot table to use for this container")]
    [SerializeField] private LootTable lootTable;
    
    [Header("Spawn Settings")]
    [Tooltip("Where items will spawn")]
    [SerializeField] private Transform spawnPoint;
    
    [Tooltip("Randomize spawn position within this radius")]
    [SerializeField] private float spawnRadius = 0.3f;
    
    [Tooltip("Spawn items slightly above spawn point to prevent clipping")]
    [SerializeField] private float spawnHeightOffset = 0.1f;
    
    [Tooltip("Apply random rotation to spawned items")]
    [SerializeField] private bool randomizeRotation = true;
    
    [Header("Container State")]
    [Tooltip("Can this container only be looted once?")]
    [SerializeField] private bool oneTimeUse = true;
    
    [Tooltip("Has this container been looted already?")]
    [SerializeField] private bool hasBeenLooted = false;
    
    [Header("Visual Feedback")]
    [Tooltip("Optional: GameObject to disable/enable when looted (e.g., lid, glow effect)")]
    [SerializeField] private GameObject visualFeedbackObject;
    
    [Header("Animation Settings")]
    [Tooltip("Optional: Animator to trigger when opening container")]
    [SerializeField] private Animator containerAnimator;
    
    [Tooltip("Animation trigger name for opening")]
    [SerializeField] private string openTriggerName = "Open";
    
    [Header("Simple Transform Opening (No Animation)")]
    [Tooltip("GameObject to move when opened (e.g., drawer, lid)")]
    [SerializeField] private Transform movingPart;
    
    [Tooltip("Local position offset to move to when opened")]
    [SerializeField] private Vector3 openPositionOffset = Vector3.zero;
    
    [Tooltip("Local rotation offset when opened (in degrees)")]
    [SerializeField] private Vector3 openRotationOffset = Vector3.zero;
    
    [Tooltip("Use smooth movement instead of instant?")]
    [SerializeField] private bool useSmoothMovement = false;
    
    [Tooltip("Movement speed if using smooth movement")]
    [SerializeField] private float movementSpeed = 2f;
    
    [Header("Audio Settings")]
    [Tooltip("Sound to play when opening container")]
    [SerializeField] private AudioClip openSound;
    
    [Tooltip("Minimum pitch variation")]
    [SerializeField] private float minPitch = 0.9f;
    
    [Tooltip("Maximum pitch variation")]
    [SerializeField] private float maxPitch = 1.1f;
    
    private Vector3 closedPosition;
    private Quaternion closedRotation;
    private bool isMoving = false;
    private bool hasStoredInitialTransform = false;
    
    private void Awake()
    {
        // Store initial transform if using moving part - do this in Awake to ensure it happens before Load()
        if (movingPart != null && !hasStoredInitialTransform)
        {
            closedPosition = movingPart.localPosition;
            closedRotation = movingPart.localRotation;
            hasStoredInitialTransform = true;
        }
    }
    
    public override void Interacted()
    {
        base.Interacted();
        
        // Check if already looted
        if (oneTimeUse && hasBeenLooted)
        {
            return;
        }
        
        // Mark as looted
        hasBeenLooted = true;
        
        // Mark as opened for save system
        SaveableContainer saveableContainer = GetComponent<SaveableContainer>();
        if (saveableContainer != null)
        {
            saveableContainer.MarkAsOpened();
        }
        
        // Open container using animation or transform
        OpenContainer();
        
        // Update visual feedback
        if (visualFeedbackObject != null)
        {
            visualFeedbackObject.SetActive(false);
        }
        
        // Roll loot and spawn items
        SpawnLoot();
        
        // Trigger interaction event
        OnInteracted?.Invoke();
        
        // Destroy container if set
        if (destroyOnInteract)
        {
            Destroy(gameObject, 0.5f); // Small delay to allow animation/sound
        }
    }
    
    private void OpenContainer()
    {
        
        // Play open sound with random pitch
        if (openSound != null)
        {
            GameObject tempAudio = new GameObject("LootContainerAudio");
            tempAudio.transform.position = transform.position;
            
            AudioSource audioSource = tempAudio.AddComponent<AudioSource>();
            audioSource.clip = openSound;
            audioSource.volume = 5f;
            float pitch = Random.Range(minPitch, maxPitch);
            audioSource.pitch = pitch;
            audioSource.spatialBlend = 0f; // 2D sound - play at full volume everywhere
            audioSource.minDistance = 1f;
            audioSource.maxDistance = 20f;
            
            audioSource.Play();
            
            Destroy(tempAudio, openSound.length + 0.5f);
        }
        else
        {
        }
        
        // Use animator if available
        if (containerAnimator != null && !string.IsNullOrEmpty(openTriggerName))
        {
            containerAnimator.SetTrigger(openTriggerName);
        }
        // Otherwise use simple transform movement
        else if (movingPart != null)
        {
            if (useSmoothMovement)
            {
                StartCoroutine(SmoothMoveContainer());
            }
            else
            {
                // Instant movement
                movingPart.localPosition = closedPosition + openPositionOffset;
                movingPart.localRotation = closedRotation * Quaternion.Euler(openRotationOffset);
            }
        }
    }
    
    private IEnumerator SmoothMoveContainer()
    {
        isMoving = true;
        
        Vector3 targetPosition = closedPosition + openPositionOffset;
        Quaternion targetRotation = closedRotation * Quaternion.Euler(openRotationOffset);
        
        float elapsed = 0f;
        float duration = 1f / movementSpeed;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            movingPart.localPosition = Vector3.Lerp(closedPosition, targetPosition, t);
            movingPart.localRotation = Quaternion.Lerp(closedRotation, targetRotation, t);
            
            yield return null;
        }
        
        // Ensure final position
        movingPart.localPosition = targetPosition;
        movingPart.localRotation = targetRotation;
        
        isMoving = false;
    }
    
    private void SpawnLoot()
    {
        if (lootTable == null)
        {
            return;
        }
        
        // Roll the loot table
        List<PrefabDrop> drops = lootTable.RollLoot();
        
        if (drops.Count == 0)
        {
            return;
        }
        
        // Spawn each prefab
        foreach (var drop in drops)
        {
            SpawnPrefab(drop.prefab);
        }
    }
    
    private void SpawnPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return;
        }
        
        // Determine spawn position
        Vector3 spawnPosition = GetSpawnPosition();
        
        // Determine spawn rotation
        Quaternion spawnRotation = randomizeRotation 
            ? Quaternion.Euler(0, Random.Range(0f, 360f), 0) 
            : Quaternion.identity;
        
        // Try to use object pooling first
        GameObject spawnedObject = null;
        
        // Check if prefab has Poolable component
        Poolable poolable = prefab.GetComponent<Poolable>();
        
        if (poolable != null)
        {
            // Use object pooling
            spawnedObject = prefab.Reuse(spawnPosition, spawnRotation);
        }
        else
        {
            // Instantiate normally
            spawnedObject = Instantiate(prefab, spawnPosition, spawnRotation);
        }
        
        // Verify the spawned object has ItemPickup or InspectableItemPickup
        if (spawnedObject != null)
        {
            bool hasPickup = spawnedObject.GetComponent<ItemPickup>() != null || spawnedObject.GetComponent<InspectableItemPickup>() != null;
            if (!hasPickup)
            {
            }
        }
    }
    
    private Vector3 GetSpawnPosition()
    {
        Vector3 basePosition = spawnPoint != null ? spawnPoint.position : transform.position;
        
        // Add random offset within radius
        if (spawnRadius > 0)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            basePosition += new Vector3(randomCircle.x, 0, randomCircle.y);
        }
        
        // Add height offset
        basePosition.y += spawnHeightOffset;
        
        return basePosition;
    }
    
    // Public method to restore container to opened state (used by save system)
    public void RestoreOpenedState()
    {
        // Mark as looted so it can't be opened again
        hasBeenLooted = true;
        
        // Update visual feedback
        if (visualFeedbackObject != null)
        {
            visualFeedbackObject.SetActive(false);
        }
        
        // Set drawer/lid to open position directly (without animation)
        if (movingPart != null)
        {
            Vector3 targetPos = closedPosition + openPositionOffset;
            
            // Use the stored closed position from Awake() plus the offset
            movingPart.localPosition = targetPos;
            movingPart.localRotation = closedRotation * Quaternion.Euler(openRotationOffset);
            
        }
        
        // Trigger animator if it exists (in case there are other visual states)
        if (containerAnimator != null && !string.IsNullOrEmpty(openTriggerName))
        {
            containerAnimator.SetTrigger(openTriggerName);
        }
        
        // Disable interaction
        if (GetComponent<Collider>() != null)
        {
            GetComponent<Collider>().enabled = false;
        }
    }
    
    // Public method to reset container (useful for testing or respawning loot)
    public void ResetContainer()
    {
        hasBeenLooted = false;
        
        if (visualFeedbackObject != null)
        {
            visualFeedbackObject.SetActive(true);
        }
        
        // Reset moving part to closed position
        if (movingPart != null)
        {
            movingPart.localPosition = closedPosition;
            movingPart.localRotation = closedRotation;
        }
        
        // Re-enable interaction
        if (GetComponent<Collider>() != null)
        {
            GetComponent<Collider>().enabled = true;
        }
    }
    
    // Editor helper - visualize spawn area
    private void OnDrawGizmosSelected()
    {
        if (spawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPoint.position + Vector3.up * spawnHeightOffset, spawnRadius);
            Gizmos.DrawLine(spawnPoint.position, spawnPoint.position + Vector3.up * 0.5f);
        }
    }
}
