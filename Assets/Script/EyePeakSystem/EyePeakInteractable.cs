using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EyePeakInteractable : Interactable
{
    public Transform normalCamPos;
    public Transform playerEnterPos;
    public Transform playerExitPos;
    public float pitchAngleMin;
    public float pitchAngleMax;
    public float yawAngleRestriction = 50f;

    Collider coll;

    private void Awake()
    {
        coll = GetComponent<Collider>();
    }

    public override void Interacted()
    {
        base.Interacted();

        //Disable any further interaction
        coll.enabled = false;

        //Enter Peak Mode
        EyePeakHandler.instance.EnterPeakMode(this);
    }
    
    public void ExitedInteraction()
    {
        //Reset Collider
        coll.enabled = true;
    }
}
