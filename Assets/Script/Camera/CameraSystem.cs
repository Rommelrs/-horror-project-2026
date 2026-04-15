using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraSystem : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] Transform m_CameraTransform;
    [SerializeField] PlayerMovement m_PlayerMovement;
    [SerializeField] PlayerWeaponSystem m_PlayerWeaponSystem;

    [Header("Follow")]
    [SerializeField] Transform followTarget;
    [SerializeField] float followSpeed = 1.0f;

    [Header("LookAt")]
    [SerializeField] Transform lookAtTarget;
    [SerializeField] float lookAtSpeed = 1.0f;
    [SerializeField] float lookAtSpeedMultiplier= 1.5f;

    [Header("Camera Collision")]
    [SerializeField] Transform m_CameraPositionOffset;
    [SerializeField] float collisionRadius = 0.2f;
    [SerializeField] float zoomSmoothTime = 0.08f;
    [SerializeField] LayerMask cameraCollisionLayer;
    [SerializeField] float zoomMin;
    [SerializeField] float zoomMax;
    [SerializeField] float collisionOffset = 0.15f;
    float zoomVelocity;

    Vector3 targetFollowPosition;
    bool strafing = true;
    Vector2 moveInput;

    private void Start()
    {
        if (Player.instance)
            Player.instance.OnPlayerTelported.AddListener(OnPlayerTeleported);
    }

    private void OnDestroy()
    {
        if (Player.instance)
            Player.instance.OnPlayerTelported.RemoveListener(OnPlayerTeleported);
    }

    void OnPlayerTeleported()
    {
        //Vector3 localPos = m_CameraPositionOffset.localPosition;
        //localPos.z = zoomMin;
        //m_CameraPositionOffset.localPosition = localPos;

        targetFollowPosition = followTarget.position;
        m_CameraTransform.position = targetFollowPosition;

        //Update rotation instantly
        m_CameraTransform.rotation = Quaternion.LookRotation(lookAtTarget.forward, Vector3.up);
    }

    private void LateUpdate()
    {
        HandlePosition();
        HandleStrafingLogic();
        HandleRotation();

        CheckCameraCollision();
    }

    void HandleStrafingLogic()
    {
        moveInput = m_PlayerMovement.GetMoveInput;
        if ((Mathf.Abs(moveInput.y) < 0.1f && Mathf.Abs(moveInput.x) > 0.1f))
            strafing = true;

        if (moveInput.y > 0.1f)
            strafing = false;

        if (m_PlayerWeaponSystem.isAiming)
            strafing = false;
    }

    void HandlePosition()
    {
        //Update Position
        //targetFollowPosition = followTarget.position + followTarget.forward * followOffset.x + followTarget.right * followOffset.y + followTarget.up * followOffset.z;
        targetFollowPosition = followTarget.position;
        m_CameraTransform.position = Vector3.Lerp(m_CameraTransform.position, targetFollowPosition, followSpeed * Time.deltaTime);
    }

    void HandleRotation()
    {
        if (!strafing)
        {
            //If Not Strafing then Update Rotation
            Quaternion rotation = Quaternion.LookRotation(lookAtTarget.forward, Vector3.up);

            if (moveInput.y > 0.1f && Mathf.Abs(moveInput.x) > 0.1f)
                m_CameraTransform.rotation = Quaternion.Lerp(m_CameraTransform.rotation, rotation, lookAtSpeed * lookAtSpeedMultiplier * Time.deltaTime);
            else
                m_CameraTransform.rotation = Quaternion.Lerp(m_CameraTransform.rotation, rotation, lookAtSpeed * Time.deltaTime);
        }
    }

    void CheckCameraCollision()
    {
        Vector3 pivotPosition = followTarget.position + new Vector3(0f, m_CameraPositionOffset.localPosition.y, 0f);
        float desiredZoom = zoomMax;
        float distance = Mathf.Abs(desiredZoom);

        Vector3 desiredWorldPos = pivotPosition + (-m_CameraTransform.forward * distance);
        Vector3 direction =  desiredWorldPos - pivotPosition;

        if (Physics.SphereCast(pivotPosition,collisionRadius,direction.normalized,out RaycastHit hit,distance,cameraCollisionLayer))
        {
            desiredZoom = -(hit.distance - collisionOffset);
            desiredZoom = Mathf.Clamp(desiredZoom, zoomMax, zoomMin);
        }

        // SmoothDamp removes jitter completely
        float smoothZoom = Mathf.SmoothDamp(m_CameraPositionOffset.localPosition.z, desiredZoom, ref zoomVelocity, zoomSmoothTime); // smooth time (tweak 0.05–0.15));

        Vector3 localPos = m_CameraPositionOffset.localPosition;
        localPos.z = smoothZoom;
        m_CameraPositionOffset.localPosition = localPos;
    }
}
