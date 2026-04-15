using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FuseboxSwitch : Interactable
{
    [Header("Switch Settings")]
    [SerializeField] bool isSwitchPulled = false;
    [SerializeField] ElectricityHazard electricityHazard; // Reference to the electricity to disable
    [SerializeField] ElectricityTrapController trapController; // Reference to stop sparks audio

    [Header("Visual")]
    [SerializeField] GameObject switchOnObject; // Visual for switch in ON position
    [SerializeField] GameObject switchOffObject; // Visual for switch in OFF position

    [Header("Audio")]
    [SerializeField] AudioClip switchPullClip;
    
    AudioSource audioSource;
    Collider coll;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        coll = GetComponent<Collider>();

        // Initialize visuals
        UpdateSwitchVisual();
    }

    public override void Interacted()
    {
        base.Interacted();

        if (isSwitchPulled)
            return;

        // Pull the switch
        PullSwitch();
    }

    void PullSwitch()
    {
        isSwitchPulled = true;

        // Update visual
        UpdateSwitchVisual();

        // Play sound
        if (audioSource != null && switchPullClip != null)
            audioSource.PlayOneShot(switchPullClip);

        // Disable electricity hazard
        if (electricityHazard != null)
        {
            electricityHazard.DeactivateElectricity();
        }

        // Stop the fusebox sparks audio
        if (trapController != null)
        {
            trapController.StopSparksAudio();
        }

        // Disable interaction
        if (coll != null)
            coll.enabled = false;

        // Call Unity Event
        OnInteracted?.Invoke();
    }

    void UpdateSwitchVisual()
    {
        if (switchOnObject != null)
            switchOnObject.SetActive(!isSwitchPulled);

        if (switchOffObject != null)
            switchOffObject.SetActive(isSwitchPulled);
    }
}
