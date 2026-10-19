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

    [Header("Backwards Footsteps")]
    [Tooltip("Volume of a backwards step. Normal footsteps play at 0.4.")]
    [SerializeField] [Range(0f, 1f)] float backFootstepVolume = 0.18f;
    [Tooltip("Pitch of a backwards step. Below 1 = deeper / softer.")]
    [SerializeField] [Range(0.5f, 1.2f)] float backFootstepPitch = 0.82f;
    [SerializeField] [Range(0f, 0.2f)] float backFootstepPitchVariation = 0.04f;
    [Tooltip("Low-pass cutoff (Hz) that muffles backwards steps. 22000 = no muffling.")]
    [SerializeField] [Range(500f, 22000f)] float backFootstepLowPass = 2800f;

    Health playerHealth;
    AudioSource audioSource;
    PlayerEventHandler playerEventHandler;
    AudioSource backStepSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        CreateBackStepSource();
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
            playerEventHandler.OnBackFootstep -= OnBackFootstep;
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
            playerEventHandler.OnBackFootstep += OnBackFootstep;
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

    // Same surface clips as a normal step, but quieter, lower and muffled
    void OnBackFootstep()
    {
        AudioClip[] clips = GetFootstepSoundsForCurrentSurface();

        if (clips != null && clips.Length > 0 && backStepSource != null)
        {
            backStepSource.pitch = backFootstepPitch + Random.Range(-backFootstepPitchVariation, backFootstepPitchVariation);
            backStepSource.PlayOneShot(clips[Random.Range(0, clips.Length)], backFootstepVolume);
        }
    }

    // Own AudioSource so the lower pitch / low-pass never touches the other player sounds
    void CreateBackStepSource()
    {
        GameObject go = new GameObject("BackFootstepAudio");
        go.transform.SetParent(transform, false);
        backStepSource = go.AddComponent<AudioSource>();
        backStepSource.playOnAwake = false;
        backStepSource.loop = false;
        if (audioSource != null)
        {
            backStepSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
            backStepSource.spatialBlend = audioSource.spatialBlend;
            backStepSource.rolloffMode = audioSource.rolloffMode;
            backStepSource.minDistance = audioSource.minDistance;
            backStepSource.maxDistance = audioSource.maxDistance;
        }
        AudioLowPassFilter lowPass = go.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = backFootstepLowPass;
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
