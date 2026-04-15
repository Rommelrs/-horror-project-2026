using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioInteraction : MonoBehaviour
{
    public AudioClip audioClip;
    public Transform interactPoint;
    public bool destroyAfterInteraction = true;

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
