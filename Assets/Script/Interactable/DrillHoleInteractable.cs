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

        Debug.Log("[DrillHole] Interacted() called on: " + gameObject.name + ". busy: " + busy);

        if (busy) return;

        StartCoroutine(Co_DrillHole());
    }

    public override void OnTriggerEnter(Collider other)
    {
        Debug.Log("[DrillHole] OnTriggerEnter: " + other.gameObject.name + " tag: " + other.gameObject.tag);
        base.OnTriggerEnter(other);
    }

    public override void OnTriggerExit(Collider other)
    {
        Debug.Log("[DrillHole] OnTriggerExit: " + other.gameObject.name);
        base.OnTriggerExit(other);
    }

    IEnumerator Co_DrillHole()
    {
        busy = true;
        coll.enabled = false;

        Debug.Log("[DrillHole] Starting drill sequence. FadeOut...");
        FadeScreenUI.instance.FadeOut();

        yield return new WaitForSeconds(drillStartDelay);

        if(audioSource != null)
        {
            audioSource.PlayOneShot(drillingSound);
        }
        else
        {
            Debug.LogWarning("[DrillHole] No AudioSource found!");
        }

        //Decrease Drill Charge
        if (Player.instance != null)
        {
            Debug.Log("[DrillHole] Removing drill charge. Charges before: " + Player.instance.inventory.GetDrillChargeCount());
            Player.instance.inventory.RemoveDrillCharge();
        }

        Debug.Log("[DrillHole] Enabling " + objectsToEnable.Length + " objects, disabling " + objectsToDisable.Length + " objects.");
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
