using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

public class AudioInteraction : MonoBehaviour
{
    public AudioClip audioClip;
    public Transform interactPoint;
    public bool destroyAfterInteraction = true;
    [Tooltip("Optional: Khalid's remark after the sound (e.g. his father's door: \"Not yet...\"). Empty = sound only.")]
    public LocalizedString line;

    private void OnTriggerEnter(Collider other)
    {
        //Player Enter Audio Interaction Collider
        if (other.gameObject.CompareTag("Player"))
        {
            AudioInteractionHandler.instance.AudioInteractionTriggerEnter(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        //Player Exit Audio Interaction Collider
        if (other.gameObject.CompareTag("Player"))
        {
            AudioInteractionHandler.instance.AudioInteractionTriggerExit(this);
        }
    }
}
