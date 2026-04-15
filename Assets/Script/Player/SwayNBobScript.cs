using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SwayNBobScript : MonoBehaviour
{
    [SerializeField] Player player;
    [SerializeField] Transform fpAimTransform;

    [Header("Sway")]
    public float step = 0.01f;
    public float maxStepDistance = 0.06f;
    Vector3 swayPos;

    [Header("Sway Rotation")]
    public float rotationStep = 4f;
    public float maxRotationStep = 5f;
    Vector3 swayEulerRot;

    public float smooth = 10f;
    float smoothRot = 12f;

    [Header("Bobbing")]
    public float speedCurve;
    float curveSin { get => Mathf.Sin(speedCurve); }
    float curveCos { get => Mathf.Cos(speedCurve); }

    public Vector3 travelLimit = Vector3.one * 0.025f;
    public Vector3 bobLimit = Vector3.one * 0.01f;
    public float bobLimitMultiplierWhileWalking = 2f;
    Vector3 bobPosition;

    public float bobExaggeration;

    [Header("Bob Rotation")]
    public Vector3 idleBobMultiplier;
    public Vector3 walkingBobMultiplier;

    public BobStabilityThreshold[] bobStabilityThresholds;
    [SerializeField] Image aimCrosshairImage;


    [System.Serializable]
    public struct BobStabilityThreshold
    {
        public float maxStability;
        public float minStability;
        public float bobMultiplier;
        public float aimCrosshairMultiplierX;
        public float aimCrosshairMultiplierY;

        public float fpAimPointRotationIntensityX;
        public float fpAimPointRotationIntensityY;
    }

    Vector3 bobEulerRotation;
    Vector2 walkInput;
    Vector2 lookInput;

    void Update()
    {
        GetInput();

        Sway();
        SwayRotation();
        BobOffset();
        BobRotation();

        CompositePositionRotation();
    }

    private void LateUpdate()
    {
        HandleCrosshairUIMovement();
    }

    void HandleCrosshairUIMovement()
    {
        if (aimCrosshairImage == null || player == null || player.playerStability == null)
            return;

        BobStabilityThreshold currentThreshold = bobStabilityThresholds[0];

        if (!player.playerStability.calmingInhalerIsActive)
            currentThreshold = GetCurrentBobStabilityThreshold();

        aimCrosshairImage.transform.localPosition = new Vector3(transform.localPosition.x * currentThreshold.aimCrosshairMultiplierX, transform.localPosition.y * currentThreshold.aimCrosshairMultiplierY, 0f);
    }

    //Get Input
    void GetInput()
    {
        if(EyePeakHandler.instance != null && EyePeakHandler.instance.IsEyePeakActivated())
        {
            lookInput = Vector2.zero;
            return;
        }

        lookInput.x = Input.GetAxis("Mouse X");
        lookInput.y = Input.GetAxis("Mouse Y");
    }

    //Sway Position Update
    void Sway()
    {
        Vector3 invertLook = lookInput * -step;
        invertLook.x = Mathf.Clamp(invertLook.x, -maxStepDistance, maxStepDistance);
        invertLook.y = Mathf.Clamp(invertLook.y, -maxStepDistance, maxStepDistance);
        swayPos = invertLook;
    }

    //Sway Rotation Update
    void SwayRotation()
    {
        Vector2 invertLook = lookInput * -rotationStep;
        invertLook.x = Mathf.Clamp(invertLook.x, -maxRotationStep, maxRotationStep);
        invertLook.y = Mathf.Clamp(invertLook.y, -maxRotationStep, maxRotationStep);
        swayEulerRot = new Vector3(invertLook.y, invertLook.x, invertLook.x);
    }

    //Update local position and rotation
    void CompositePositionRotation()
    {
        transform.localPosition = Vector3.Lerp(transform.localPosition, swayPos + bobPosition, Time.deltaTime * smooth);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(swayEulerRot) * Quaternion.Euler(bobEulerRotation), Time.deltaTime * smoothRot);

        if (fpAimTransform != null)
        {
            BobStabilityThreshold bobStability = GetCurrentBobStabilityThreshold();
            Vector3 fpAimEularAngle = bobEulerRotation;
            fpAimEularAngle.x = fpAimEularAngle.x * bobStability.fpAimPointRotationIntensityX;
            fpAimEularAngle.y = fpAimEularAngle.y * bobStability.fpAimPointRotationIntensityY;
            fpAimEularAngle.z = 0f;

            fpAimTransform.transform.localRotation = Quaternion.Euler(fpAimEularAngle);
        }
    }

    //Apply Bob offset
    void BobOffset()
    {
        bool isGrounded = player.controller.isGrounded;
        // Vector2 moveInput = player.playerMovement.GetMoveInput;
        Vector2 moveInput = Vector2.zero;
        
        speedCurve += Time.deltaTime * (isGrounded ? (moveInput .x + moveInput.y) * bobExaggeration : 1f) + 0.01f;

        if (moveInput.magnitude > 0.1f)
        {
            bobPosition.x = (curveCos * bobLimit.x * bobLimitMultiplierWhileWalking * (isGrounded ? 1 : 0)) - (walkInput.x * travelLimit.x);
            bobPosition.y = (curveSin * bobLimit.y * bobLimitMultiplierWhileWalking) - (moveInput.y * travelLimit.y);
            bobPosition.z = -(walkInput.y * travelLimit.z);
        }
        else
        {
            bobPosition.x = (curveCos * bobLimit.x * (isGrounded ? 1 : 0)) - (walkInput.x * travelLimit.x);
            bobPosition.y = (curveSin * bobLimit.y) - (moveInput.y * travelLimit.y);
            bobPosition.z = -(walkInput.y * travelLimit.z);
        }
    }

    BobStabilityThreshold GetCurrentBobStabilityThreshold()
    {
        if (player == null || player.playerStability == null || bobStabilityThresholds == null || bobStabilityThresholds.Length == 0)
            return new BobStabilityThreshold();

        foreach (BobStabilityThreshold bobThreshold in bobStabilityThresholds)
        {
            if (player.playerStability.stability >= bobThreshold.minStability && player.playerStability.stability < bobThreshold.maxStability)
            {
                return bobThreshold;
            }
        }

        return bobStabilityThresholds[0];
    }


    //Apply Bob rotation
    void BobRotation()
    {
        float currentBobMultiplier = 1.0f;
        if (!player.playerStability.calmingInhalerIsActive)
        {
            foreach (BobStabilityThreshold bobThreshold in bobStabilityThresholds)
            {
                if (player.playerStability.stability >= bobThreshold.minStability && player.playerStability.stability < bobThreshold.maxStability)
                {
                    currentBobMultiplier = bobThreshold.bobMultiplier;
                    break;
                }
            }
        }

        if (walkInput != Vector2.zero)
        {
            //Walking
            bobEulerRotation.x = walkingBobMultiplier.x * Mathf.Sin(2 * speedCurve) * currentBobMultiplier;
            bobEulerRotation.y = walkingBobMultiplier.y * curveCos * currentBobMultiplier;
            bobEulerRotation.z = walkingBobMultiplier.z * curveCos * walkInput.x * currentBobMultiplier;
        }
        else
        {
            //Not Walking
            bobEulerRotation.x = idleBobMultiplier.x * Mathf.Sin(speedCurve) * currentBobMultiplier;
            bobEulerRotation.y = idleBobMultiplier.y * curveCos * currentBobMultiplier;
            bobEulerRotation.z = idleBobMultiplier.z * curveCos * currentBobMultiplier;
        }
    }
}
