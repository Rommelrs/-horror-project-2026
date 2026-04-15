using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AmbienceTrigger : MonoBehaviour
{
    public AmbienceHandler.AmbienceType ambienceType;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            AmbienceHandler.Instance.UpdateAmbience(ambienceType);
        }
    }
}
