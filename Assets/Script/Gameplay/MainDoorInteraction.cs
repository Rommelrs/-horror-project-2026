using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainDoorInteraction : MonoBehaviour
{
    public DoorInteractable doorInteractable;
    public SubtitleTrigger shootSubtitleTrigger;

    public GameObject []objectsToDisable;
    public GameObject[] objectsToEnable;
    public ParticleSystem finalDustParticle;

    bool enemyDied = false;

    public void SetEnemyDied()
    {
        enemyDied = true;
    }

    public void OnDoorInteractionSuccess()
    {
        if(finalDustParticle != null)
        {
            finalDustParticle.gameObject.SetActive(true);
            finalDustParticle.Play();
        }

        foreach (GameObject item in objectsToDisable)
        {
            item.SetActive(false);
        }

        foreach (GameObject item in objectsToEnable)
        {
            item.SetActive(true);
        }
    }

    public void OnDoorInteractionFailed()
    {
        if (enemyDied)
            shootSubtitleTrigger.TriggerSubtitle();
    }
}
