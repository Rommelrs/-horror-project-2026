using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrillHoleInteractable : Interactable
{
    [SerializeField] float drillStartDelay = 1f;
    [SerializeField] float drillDuration = 5f;
    [SerializeField] float destoryDelay = 1f;

    [SerializeField] GameObject []objectsToDisable;
    [SerializeField] GameObject []objectsToEnable;

    [SerializeField] AudioClip drillingSound;

    bool busy = false;
    Collider coll;
    AudioSource audioSource;

    private void Awake()
    {
        coll = GetComponent<Collider>();
        audioSource = GetComponent<AudioSource>();
    }

    public override void Interacted()
    {
        base.Interacted();

        if (busy) return;

        StartCoroutine(Co_DrillHole());
    }

    IEnumerator Co_DrillHole()
    {
        busy = true;
        coll.enabled = false;

        FadeScreenUI.instance.FadeOut();

        yield return new WaitForSeconds(drillStartDelay);

        if(audioSource != null)
        {
            audioSource.PlayOneShot(drillingSound);
        }

        //Decrease Drill Charge
        if (Player.instance != null)
            Player.instance.inventory.RemoveDrillCharge();

        foreach (var item in objectsToEnable)
        {
            item.gameObject.SetActive(true);
        }

        foreach (var item in objectsToDisable)
        {
            item.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(drillDuration);

        FadeScreenUI.instance.FadeIn();

        yield return new WaitForSeconds(destoryDelay);

        Destroy(this.gameObject);
    }

    public bool HasDrill()
    {
        return Player.instance.inventory.HasDrill();
    }

    public int DrillChargeCount()
    {
        return Player.instance.inventory.GetDrillChargeCount();
    }
}
