using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElectricityHazard : MonoBehaviour
{
    [Header("Electricity Settings")]
    [SerializeField] bool isActive = false;
    [SerializeField] GameObject electricityVisual; // Visual effect (particle system, mesh, etc.)
    [SerializeField] GameObject damageZoneObject; // GameObject with DamageOnTrigger and collider
    
    DamageOnTrigger damageZone; // Reference cached from damageZoneObject
    
    [Header("Audio")]
    [SerializeField] AudioSource electricityAudioSource;
    [SerializeField] AudioClip electricityLoopClip;
    [SerializeField] AudioClip activationClip;
    [SerializeField] AudioClip deactivationClip;

    [Header("Auto Retreat Settings")]
    [SerializeField] float retreatSpeed = 3f;
    [SerializeField] float retreatDuration = 0.5f;

    private void Start()
    {
        // Cache damage zone component
        if (damageZoneObject != null)
            damageZone = damageZoneObject.GetComponent<DamageOnTrigger>();

        // Initialize state
        if (isActive)
            ActivateElectricity();
        else
            DeactivateElectricity();
    }

    public void ActivateElectricity()
    {
        if (isActive)
            return;

        isActive = true;

        // Enable visual effect
        if (electricityVisual != null)
            electricityVisual.SetActive(true);

        // Enable damage zone GameObject
        if (damageZoneObject != null)
            damageZoneObject.SetActive(true);

        // Play activation sound
        if (electricityAudioSource != null && activationClip != null)
            electricityAudioSource.PlayOneShot(activationClip);

        // Start looping electricity sound
        if (electricityAudioSource != null && electricityLoopClip != null)
        {
            electricityAudioSource.clip = electricityLoopClip;
            electricityAudioSource.loop = true;
            electricityAudioSource.Play();
        }
    }

    public void DeactivateElectricity()
    {
        isActive = false;

        // Disable visual effect
        if (electricityVisual != null)
            electricityVisual.SetActive(false);

        // Disable damage zone GameObject
        if (damageZoneObject != null)
            damageZoneObject.SetActive(false);

        // Stop looping sound
        if (electricityAudioSource != null && electricityAudioSource.isPlaying)
        {
            electricityAudioSource.Stop();
        }

        // Play deactivation sound
        if (electricityAudioSource != null && deactivationClip != null)
            electricityAudioSource.PlayOneShot(deactivationClip);
    }

    public bool IsActive()
    {
        return isActive;
    }

    // Call this when player takes damage from electricity to make them walk backward
    public void RetreatPlayer()
    {
        if (Player.instance != null)
        {
            StartCoroutine(ApplyAutoRetreat());
        }
    }

    private IEnumerator ApplyAutoRetreat()
    {
        Player player = Player.instance;
        if (player == null)
            yield break;

        float elapsed = 0f;
        
        while (elapsed < retreatDuration)
        {
            // Move player backward (simulate pressing S key)
            if (player.GetComponent<CharacterController>() != null)
            {
                CharacterController controller = player.GetComponent<CharacterController>();
                // Move backward relative to player's forward direction
                Vector3 backwardDirection = -player.transform.forward;
                controller.Move(backwardDirection * retreatSpeed * Time.deltaTime);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
