using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoRetreatTrigger : MonoBehaviour
{
    [Header("Retreat Settings")]
    [SerializeField] float retreatSpeed = 3f;
    [SerializeField] float retreatDuration = 0.5f;
    [SerializeField] bool useBackwardDirection = true; // If true, pushes player backward. If false, pushes away from trigger center
    [SerializeField] bool disablePlayerControls = true; // Disable player movement during retreat
    [SerializeField] float triggerCooldown = 1f; // Cooldown before trigger can activate again
    
    [Header("Audio (Optional)")]
    [SerializeField] AudioClip retreatSound;
    
    private bool isRetreating = false;
    private float lastTriggerTime = -999f;
    
    private void OnTriggerEnter(Collider other)
    {
        // Check cooldown to prevent rapid re-triggering
        if (other.CompareTag("Player") && !isRetreating && Time.time >= lastTriggerTime + triggerCooldown)
        {
            RetreatPlayer();
        }
    }
    
    private void RetreatPlayer()
    {
        if (Player.instance != null)
        {
            lastTriggerTime = Time.time;
            StartCoroutine(ApplyAutoRetreat());
            
            // Play sound if assigned
            if (retreatSound != null && SoundEffectManager.instance != null)
            {
                SoundEffectManager.instance.PlaySFXAtPosition(retreatSound, transform.position);
            }
        }
    }
    
    private IEnumerator ApplyAutoRetreat()
    {
        isRetreating = true;
        Player player = Player.instance;
        
        if (player == null)
        {
            isRetreating = false;
            yield break;
        }
        
        // Disable player controls if enabled
        bool controlsWereEnabled = false;
        if (disablePlayerControls && player.playerMovement != null)
        {
            controlsWereEnabled = player.playerMovement.enabled;
            player.playerMovement.enabled = false;
        }
        
        float elapsed = 0f;
        
        while (elapsed < retreatDuration)
        {
            if (player.GetComponent<CharacterController>() != null)
            {
                CharacterController controller = player.GetComponent<CharacterController>();
                
                Vector3 retreatDirection;
                if (useBackwardDirection)
                {
                    // Move backward relative to player's forward direction
                    retreatDirection = -player.transform.forward;
                }
                else
                {
                    // Push away from trigger center
                    retreatDirection = (player.transform.position - transform.position).normalized;
                }
                
                controller.Move(retreatDirection * retreatSpeed * Time.deltaTime);
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Re-enable player controls
        if (disablePlayerControls && player.playerMovement != null && controlsWereEnabled)
        {
            player.playerMovement.enabled = true;
        }
        
        isRetreating = false;
    }
}
