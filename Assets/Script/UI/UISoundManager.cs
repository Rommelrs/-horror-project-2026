using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
