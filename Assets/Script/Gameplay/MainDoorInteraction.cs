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
        // A door with the unlock step says "It's locked." itself; a second subtitle on top of it would replace it (and one of the two
        // freezes the game while the other does not).
        DoorInteractable door = doorInteractable != null ? doorInteractable : GetComponent<DoorInteractable>();
        if (door != null && door.unlockStepWithKey)
            return;

        if (enemyDied)
            shootSubtitleTrigger.TriggerSubtitle();
    }
}
