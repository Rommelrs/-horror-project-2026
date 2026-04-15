using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SurfaceFootstepSounds
{
    public SurfaceType surfaceType;
    public AudioClip[] footstepSounds;
}

public class PlayerSound : MonoBehaviour
{
    [SerializeField] AudioClip[] takeDamageSounds;
    
    [Header("Footstep Sounds")]
    [SerializeField] AudioClip[] defaultFootstepSounds;
    [SerializeField] SurfaceFootstepSounds[] surfaceFootstepSounds;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float groundCheckDistance = 1.5f;

    Health playerHealth;
    AudioSource audioSource;
    PlayerEventHandler playerEventHandler;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        playerHealth = GetComponent<Health>();
        playerEventHandler = GetComponentInChildren<PlayerEventHandler>();
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (playerHealth != null)
            playerHealth.OnDamageTaken -= TakeDamage;

        if (playerEventHandler != null)
        {
            playerEventHandler.OnFootstep -= OnFootstep;
        }
    }

    private void Start()
    {
        // Subscribe to events
        if (playerHealth != null)
            playerHealth.OnDamageTaken += TakeDamage;

        if (playerEventHandler != null)
        {
            playerEventHandler.OnFootstep += OnFootstep;
        }
    }

    // Play the take damage sound
    void TakeDamage(int damage)
    {
        int randomIndex = Random.Range(0, takeDamageSounds.Length);
        audioSource.PlayOneShot(takeDamageSounds[randomIndex]);
    }

    // Play the footstep sound
    void OnFootstep()
    {
        AudioClip[] clips = GetFootstepSoundsForCurrentSurface();
        
        if (clips != null && clips.Length > 0)
        {
            int randomIndex = Random.Range(0, clips.Length);
            audioSource.PlayOneShot(clips[randomIndex], 0.4f);
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
                // Find matching surface sounds
                for (int i = 0; i < surfaceFootstepSounds.Length; i++)
                {
                    if (surfaceFootstepSounds[i].surfaceType == groundType.surfaceType)
                    {
                        return surfaceFootstepSounds[i].footstepSounds;
                    }
                }
            }
        }

        // Return default sounds if no match found
        return defaultFootstepSounds;
    }
}
