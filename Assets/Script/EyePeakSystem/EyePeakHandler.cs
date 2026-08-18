using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using UnityEngine.InputSystem;

public class EyePeakHandler : MonoBehaviour
{
    public static EyePeakHandler instance;

    [SerializeField] CinemachineVirtualCamera normalPeakCam;
    [SerializeField] InputActionReference zoomInput;
    [SerializeField] InputActionReference movementAction;
    [SerializeField] float moveSensitivity = 1f;
    [SerializeField] Vector2 movementOffsetLimit;

    bool peakModeActivated = false;
    EyePeakInteractable currentInteraction;
    Vector2 moveInput;
    Vector2 currentOffset;
    bool isAiming = false;

    private void Awake()
    {
        instance = this;
    }

    private void Update()
    {
        //Check if Peak Mode is activated
        if (peakModeActivated)
        {
            //Movement Input
            moveInput = movementAction.action.ReadValue<Vector2>();
            
            if(moveInput.magnitude > 0.1f)
            {
                currentOffset += moveInput * moveSensitivity * Time.deltaTime;

                if (currentOffset.x >= movementOffsetLimit.x)
                    currentOffset.x = movementOffsetLimit.x;

                if (currentOffset.x <= -movementOffsetLimit.x)
                    currentOffset.x = -movementOffsetLimit.x;

                if (currentOffset.y >= movementOffsetLimit.y)
                    currentOffset.y = movementOffsetLimit.y;

                if (currentOffset.y <= -movementOffsetLimit.y)
                    currentOffset.y = -movementOffsetLimit.y;
            }

            if (currentInteraction != null && currentInteraction.normalCamPos != null)
            {
                Vector3 newOffset = currentOffset.x * currentInteraction.normalCamPos.right + currentOffset.y * currentInteraction.normalCamPos.up;
                //newOffset.z = 0f;
                normalPeakCam.transform.position = currentInteraction.normalCamPos.position + newOffset;
                normalPeakCam.transform.rotation = currentInteraction.normalCamPos.rotation;
            }

            //Check if zoom input is pressed
            if (zoomInput.action.ReadValue<float>() > 0.1f)
            {
                //Zoom Cam
                normalPeakCam.Priority = 0;

                if(isAiming == false)
                {
                    isAiming = true;

                    //Set Player Pos
                    Player.instance.transform.position = currentInteraction.playerEnterPos.position;
                    Player.instance.transform.rotation = currentInteraction.playerEnterPos.rotation;
                }
            }
            else
            {
                //Default Cam
                normalPeakCam.Priority = 100;

                if(isAiming == true)
                {
                    isAiming = false;

                    //Set Player Pos
                    Player.instance.transform.position = currentInteraction.playerExitPos.position;
                    Player.instance.transform.rotation = currentInteraction.playerExitPos.rotation;
                }
            }
        }
    }

    public bool IsEyePeakActivated()
    {
        return peakModeActivated;
    }

    //Enter Peak Mode called from EyePeakInteractable
    public void EnterPeakMode(EyePeakInteractable eyePeakInteractable)
    {
        currentInteraction = eyePeakInteractable;

        peakModeActivated = true;
        currentOffset = Vector2.zero;

        isAiming = false;

        //Set Player Pos
        Player.instance.transform.position = eyePeakInteractable.playerExitPos.position;
        Player.instance.transform.rotation = eyePeakInteractable.playerExitPos.rotation;

        //Restrict Yaw rotation
        Player.instance.playerWeaponSystem.eyePeakAimingRotationEnabled = true;

        float minAngle = eyePeakInteractable.playerEnterPos.transform.eulerAngles.y - eyePeakInteractable.yawAngleRestriction;
        float maxAngle = eyePeakInteractable.playerEnterPos.transform.eulerAngles.y + eyePeakInteractable.yawAngleRestriction;

        Player.instance.playerWeaponSystem.restrictYawAngleMin = minAngle;
        Player.instance.playerWeaponSystem.restrictYawAngleMax = maxAngle;

        Player.instance.playerWeaponSystem.restrictPitchAngleMin = eyePeakInteractable.pitchAngleMin;
        Player.instance.playerWeaponSystem.restrictPitchAngleMax = eyePeakInteractable.pitchAngleMax;

        normalPeakCam.Priority = 100;
    }

    //Exit Peak Mode
    public void ExitPeakMode()
    {
        peakModeActivated = false;
        normalPeakCam.Priority = 0;

        if (currentInteraction != null)
        {
            //Set Player Pos
            Player.instance.transform.position = currentInteraction.playerExitPos.position;
            Player.instance.transform.rotation = currentInteraction.playerExitPos.rotation;

            //Reset Restrict Yaw rotation
            Player.instance.playerWeaponSystem.eyePeakAimingRotationEnabled = false;

            currentInteraction.ExitedInteraction();

            currentInteraction = null;
        }
    }
}
