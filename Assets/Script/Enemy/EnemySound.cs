using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySound : MonoBehaviour
{
    [Header("Footstep Sounds")]
    [SerializeField] AudioClip[] defaultFootstepSounds;
    [SerializeField] SurfaceFootstepSounds[] surfaceFootstepSounds;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float groundCheckDistance = 1.5f;
    [SerializeField] [Range(0f, 1f)] float footstepVolume = 0.5f;

    AudioSource audioSource;
    Enemy enemy;

    private void Awake()
    {
        // Try to get AudioSource from this GameObject or parent
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = GetComponentInParent<AudioSource>();
        
        // Try to get Enemy from this GameObject or parent
        enemy = GetComponent<Enemy>();
        if (enemy == null)
            enemy = GetComponentInParent<Enemy>();
        
        // If no audio source found, add one to the root enemy GameObject
        if (audioSource == null)
        {
            if (enemy != null)
            {
                audioSource = enemy.gameObject.AddComponent<AudioSource>();
            }
            else
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.minDistance = 1f;
            audioSource.maxDistance = 15f;
        }
    }

    // Called from animation event
    public void OnFootstep()
    {
        if (audioSource == null)
        {
            Debug.LogError($"EnemySound on {gameObject.name}: AudioSource is null!");
            return;
        }

        AudioClip[] clips = GetFootstepSoundsForCurrentSurface();
        
        if (clips != null && clips.Length > 0)
        {
            int randomIndex = Random.Range(0, clips.Length);
            audioSource.PlayOneShot(clips[randomIndex], footstepVolume);
        }
        else
        {
            Debug.LogWarning($"EnemySound on {gameObject.name}: No footstep sounds configured!");
        }
    }

    AudioClip[] GetFootstepSoundsForCurrentSurface()
    {
        // Raycast down to detect ground
        bool didHit;
        RaycastHit hit;
        
        // If ground layer is set, use it. Otherwise raycast on all layers.
        if (groundLayer != 0)
            didHit = Physics.Raycast(transform.position, Vector3.down, out hit, groundCheckDistance, groundLayer);
        else
            didHit = Physics.Raycast(transform.position, Vector3.down, out hit, groundCheckDistance);
        
        if (didHit)
        {
            // Check if the ground has a GroundType component
            GroundType groundType = hit.collider.GetComponent<GroundType>();
            
            if (groundType != null)
            {
                return GetFootstepSoundsForSurface(groundType.surfaceType);
            }
        }

        // Fallback to Stats footstep sounds if available
        if (enemy != null && enemy.stats != null && enemy.stats.footstepSounds != null && enemy.stats.footstepSounds.Length > 0)
        {
            return enemy.stats.footstepSounds;
        }

        // Return default sounds if no match found
        return defaultFootstepSounds;
    }

    // Public helper method for Enemy class to use
    public AudioClip[] GetFootstepSoundsForSurface(SurfaceType surfaceType)
    {
        // Find matching surface sounds
        for (int i = 0; i < surfaceFootstepSounds.Length; i++)
        {
            if (surfaceFootstepSounds[i].surfaceType == surfaceType)
            {
                return surfaceFootstepSounds[i].footstepSounds;
            }
        }
        
        // Return default if no match
        return defaultFootstepSounds;
    }
}
