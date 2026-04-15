using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElectricityTrapController : MonoBehaviour
{
    [Header("Trap Components")]
    [SerializeField] ElectricityHazard electricityHazard;
    [SerializeField] FuseboxSwitch fuseboxSwitch;

    [Header("Fusebox Visual")]
    [SerializeField] GameObject normalFusebox; // Normal closed fusebox
    [SerializeField] GameObject brokenFusebox; // Broken open fusebox with exposed wires

    [Header("Trigger Settings")]
    [SerializeField] bool trapActivated = false;
    [SerializeField] float fuseboxOpenDelay = 0.5f; // Delay before fusebox pops open

    [Header("Audio")]
    [SerializeField] AudioSource fuseboxAudioSource;
    [SerializeField] AudioClip fuseboxPopClip; // Sound when fusebox pops open
    [SerializeField] AudioClip fuseboxSparksClip; // Sparking sound

    [Header("Camera Shake")]
    [SerializeField] bool useCameraShake = true;
    [SerializeField] CameraShaker cameraShaker; // Reference to camera shaker
    [SerializeField] float shakeIntensity = 0.3f;
    [SerializeField] float shakeDuration = 0.5f;

    private void OnTriggerEnter(Collider other)
    {
        if (trapActivated)
            return;
        
        // Check if already triggered in save system
        SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
        if (saveableTrigger != null && saveableTrigger.WasAlreadyTriggered())
        {
            trapActivated = true; // Mark as activated to prevent re-trigger
            return;
        }
        
        if (other.CompareTag("Player"))
        {
            ActivateTrap();
        }
    }

    void ActivateTrap()
    {
        trapActivated = true;
        
        // Mark as triggered in save system
        SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
        if (saveableTrigger != null)
        {
            saveableTrigger.MarkAsTriggered();
        }
        
        StartCoroutine(Co_TrapSequence());
    }

    IEnumerator Co_TrapSequence()
    {
        // Wait a moment
        yield return new WaitForSeconds(fuseboxOpenDelay);

        // Pop open the fusebox
        OpenFusebox();

        // Play fusebox pop sound
        if (fuseboxAudioSource != null && fuseboxPopClip != null)
            fuseboxAudioSource.PlayOneShot(fuseboxPopClip);

        // Camera shake
        if (useCameraShake && cameraShaker != null)
            cameraShaker.ApplyShake(shakeIntensity, shakeDuration);

        // Small delay before sparks
        yield return new WaitForSeconds(0.3f);

        // Play sparking sound
        if (fuseboxAudioSource != null && fuseboxSparksClip != null)
            fuseboxAudioSource.PlayOneShot(fuseboxSparksClip);

        // Activate electricity
        if (electricityHazard != null)
            electricityHazard.ActivateElectricity();
    }

    void OpenFusebox()
    {
        // Swap fusebox models
        if (normalFusebox != null)
            normalFusebox.SetActive(false);

        if (brokenFusebox != null)
            brokenFusebox.SetActive(true);
    }

    // Call this method from DamageOnTrigger when player takes damage
    public void OnPlayerDamaged()
    {
        if (electricityHazard != null && electricityHazard.IsActive())
        {
            electricityHazard.RetreatPlayer();
        }
    }

    // Call this when electricity is deactivated (e.g., from FuseboxSwitch)
    public void StopSparksAudio()
    {
        if (fuseboxAudioSource != null)
            fuseboxAudioSource.Stop();
    }

    // Optional: Method to manually activate trap (can be called from Unity Events)
    public void ManualActivate()
    {
        if (!trapActivated)
            ActivateTrap();
    }
}
