using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FixedCameraAngleHandler : MonoBehaviour
{
    public static FixedCameraAngleHandler instance;

    CinemachineVirtualCamera lastVCam;

    private void Awake()
    {
        instance = this;
    }

    public void FixedCameraAngleZoneTriggerEnter(FixedCameraAngleZone fixedCameraAngleZone)
    {
        if (lastVCam != null)
            lastVCam.Priority = 0;

        fixedCameraAngleZone.vCam.Priority = 10;
        lastVCam = fixedCameraAngleZone.vCam;
    }

    public void FixedCameraAngleZoneTriggerExit(FixedCameraAngleZone fixedCameraAngleZone)
    {
        if (fixedCameraAngleZone != null)
            fixedCameraAngleZone.vCam.Priority = 0;
    }
}
