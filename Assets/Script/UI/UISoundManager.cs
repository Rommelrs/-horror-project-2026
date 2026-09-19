using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class UISoundManager : MonoBehaviour
{
    public static UISoundManager instance;
    [SerializeField] AudioClip buttonHoverSound;
    //[SerializeField] AudioClip buttonHoverExitSound;
    [SerializeField] AudioClip buttonClickSound;

    AudioSource audioSource;

    private void Awake()
    {
        instance = this;
        audioSource = GetComponent<AudioSource>();
    }

    // The Options panel (OptionsManager) starts disabled and only applies the saved
    // mixer volumes once the player opens it for the first time, so apply them here
    // too. Must run in Start (not Awake) - the audio system resets exposed mixer
    // parameters to their default when it initializes between Awake and the first
    // frame, which would otherwise wipe out a value set in Awake.
    private void Start()
    {
        AudioMixerGroup mixerGroup = audioSource.outputAudioMixerGroup;
        if (mixerGroup == null)
            return;

        AudioMixer mixer = mixerGroup.audioMixer;
        mixer.SetFloat("SFXVolume", Mathf.Log10(PlayerPrefs.GetFloat("SFXVolume", 0.85f)) * 20);
        mixer.SetFloat("MusicVolume", Mathf.Log10(PlayerPrefs.GetFloat("MusicVolume", 0.85f)) * 20);
    }

    //Player Hover Sound Effect
    public void PlayButtonHoverSound()
    {
        if (buttonHoverSound != null)
            audioSource.PlayOneShot(buttonHoverSound);
    }

    //Player Hover Exit Sound Effect
    public void PlayButtonHoverExitSound()
    {
       //if (buttonHoverExitSound != null)
       //    audioSource.PlayOneShot(buttonHoverExitSound);
    }

    //Player Click Sound Effect
    public void PlayButtonClickSound()
    {
        if (buttonClickSound != null)
            audioSource.PlayOneShot(buttonClickSound);
    }
}
