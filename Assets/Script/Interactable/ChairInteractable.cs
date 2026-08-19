using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class ChairInteractable : Interactable
{
    [Header("Fade")]
    [Tooltip("How long to wait after fade out before teleporting. Should match FadeScreenUI's fadeDuration.")]
    [SerializeField] float fadeWaitDuration = 0.5f;

    [Header("Chair Setup")]
    [Tooltip("Where the camera is positioned when sitting. Rotate it to face the direction you want.")]
    [SerializeField] Transform sitCamPos;
    [Tooltip("Where the player body sits (for teleport).")]
    [SerializeField] Transform sitPoint;
    [Tooltip("Where the player is moved when standing up.")]
    [SerializeField] Transform standUpPoint;
    [Tooltip("The Cinemachine Virtual Camera used while sitting.")]
    [SerializeField] CinemachineVirtualCamera sitCam;
    [Tooltip("Can the player stand up by pressing E?")]
    [SerializeField] bool canStandUp = true;

    [Header("Look Settings")]
    [Tooltip("How fast the camera rotates with WASD.")]
    [SerializeField] float rotationSpeed = 60f;
    [Tooltip("How far left/right the camera can rotate (degrees).")]
    [SerializeField] float yawLimit = 80f;
    [Tooltip("How far up the camera can rotate (degrees).")]
    [SerializeField] float pitchMax = 40f;
    [Tooltip("How far down the camera can rotate (degrees).")]
    [SerializeField] float pitchMin = -30f;

    [Header("Input")]
    [SerializeField] InputActionReference movementAction;
    [SerializeField] InputActionReference adsInput;
    [SerializeField] InputActionReference exitSitInput;

    [Header("Events")]
    public UnityEvent OnSitDown;
    public UnityEvent OnStandUp;

    bool isSitting = false;
    bool isTransitioning = false;
    bool wasADS = false;
    Collider triggerCollider;
    float currentYaw = 0f;
    float currentPitch = 0f;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (sitCam != null) sitCam.Priority = 0;
    }

    private void LateUpdate()
    {
        if (!isSitting || sitCamPos == null || sitCam == null) return;

        Vector2 moveInput = movementAction.action.ReadValue<Vector2>();

        // A/D rotates left/right, W/S rotates up/down
        currentYaw += moveInput.x * rotationSpeed * Time.deltaTime;
        currentPitch -= moveInput.y * rotationSpeed * Time.deltaTime;

        currentYaw = Mathf.Clamp(currentYaw, -yawLimit, yawLimit);
        currentPitch = Mathf.Clamp(currentPitch, pitchMin, pitchMax);

        // Compute current rotation from WASD
        Quaternion sitRotation = sitCamPos.rotation * Quaternion.Euler(currentPitch, currentYaw, 0f);

        // Always keep sitCam at correct position/rotation
        sitCam.transform.position = sitCamPos.position;
        sitCam.transform.rotation = sitRotation;

        // Rotate player body to always face where the camera is looking
        float worldYaw = sitCamPos.eulerAngles.y + currentYaw;
        Player.instance.transform.rotation = Quaternion.Euler(0f, worldYaw, 0f);
        Player.instance.playerWeaponSystem.CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
        Player.instance.animator.SetFloat("x", 0f);
        Player.instance.animator.SetFloat("y", 0f);
        Player.instance.animator.SetFloat("Velocity", 0f);

        bool isADS = adsInput != null && adsInput.action.ReadValue<float>() > 0.1f;

        // Only teleport player on ADS transition, not every frame
        if (isADS && !wasADS)
        {
            // Entering ADS - move player so CinemachineCameraTarget aligns with sitCamPos
            Player.instance.controller.enabled = false;
            Transform camTarget = Player.instance.playerWeaponSystem.CinemachineCameraTarget.transform;
            Vector3 camTargetOffset = camTarget.position - Player.instance.transform.position;
            Player.instance.transform.position = sitCamPos.position - camTargetOffset;
            Player.instance.controller.enabled = true;
        }
        else if (!isADS && wasADS)
        {
            // Releasing ADS - return player to sit point
            Player.instance.controller.enabled = false;
            Player.instance.transform.position = sitPoint.position;
            Player.instance.transform.rotation = sitPoint.rotation;
            Player.instance.controller.enabled = true;
        }

        wasADS = isADS;
        sitCam.Priority = isADS ? 0 : 100;
    }

    public override void Interacted()
    {
        base.Interacted();
        if (isSitting || isTransitioning) return;
        StartCoroutine(Co_SitDown());
    }

    IEnumerator Co_SitDown()
    {
        isTransitioning = true;
        currentYaw = 0f;
        currentPitch = 0f;

        if (triggerCollider != null) triggerCollider.enabled = false;

        Player.instance.pauseMovement = true;

        // Fade out first, then teleport
        FadeScreenUI.instance.FadeOut();
        yield return new WaitForSeconds(fadeWaitDuration);

        Player.instance.controller.enabled = false;
        yield return new WaitForEndOfFrame();

        Player.instance.transform.position = sitPoint.position;
        Player.instance.transform.rotation = sitPoint.rotation;
        yield return new WaitForEndOfFrame();

        Player.instance.controller.enabled = true;

        // Only now activate sitting mode (starts LateUpdate)
        isSitting = true;
        isTransitioning = false;

        // Hide player model while sitting
        if (Player.instance.playerModel != null)
            Player.instance.playerModel.SetActive(false);

        // Keep weapon enabled for ADS + shooting, but lock mouse camera movement
        Player.instance.playerWeaponSystem.weaponIsEnabled = true;
        Player.instance.playerWeaponSystem.LockCameraPosition = true;

        FadeScreenUI.instance.FadeIn();
        OnSitDown?.Invoke();

        if (canStandUp && exitSitInput != null)
            exitSitInput.action.performed += OnExitPressed;
    }

    void OnExitPressed(InputAction.CallbackContext ctx)
    {
        if (!isSitting || isTransitioning) return;
        StartCoroutine(Co_StandUp());
    }

    IEnumerator Co_StandUp()
    {
        if (exitSitInput != null)
            exitSitInput.action.performed -= OnExitPressed;

        isTransitioning = true;

        // Fade out while sitCam is still active
        FadeScreenUI.instance.FadeOut();
        yield return new WaitForSeconds(fadeWaitDuration);

        // Screen is black - now disable sitCam and stop LateUpdate
        isSitting = false;
        wasADS = false;
        if (sitCam != null) sitCam.Priority = 0;

        yield return new WaitForEndOfFrame();

        if (standUpPoint != null)
        {
            Player.instance.controller.enabled = false;
            yield return new WaitForEndOfFrame();
            Player.instance.transform.position = standUpPoint.position;
            Player.instance.transform.rotation = standUpPoint.rotation;
            yield return new WaitForEndOfFrame();
            Player.instance.controller.enabled = true;
        }

        // Restore player model
        if (Player.instance.playerModel != null)
            Player.instance.playerModel.SetActive(true);

        Player.instance.pauseMovement = false;
        Player.instance.playerWeaponSystem.LockCameraPosition = false;
        isTransitioning = false;

        if (triggerCollider != null) triggerCollider.enabled = true;

        FadeScreenUI.instance.FadeIn();
        OnStandUp?.Invoke();
    }

    public void ForceStandUp()
    {
        if (isSitting) StartCoroutine(Co_StandUp());
    }

    public bool IsSitting() => isSitting;
}
