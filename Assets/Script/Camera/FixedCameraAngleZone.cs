using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class FixedCameraAngleZone : MonoBehaviour
{
    public CinemachineVirtualCamera vCam;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            FixedCameraAngleHandler.instance.FixedCameraAngleZoneTriggerEnter(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            FixedCameraAngleHandler.instance.FixedCameraAngleZoneTriggerExit(this);
        }
    }
}
