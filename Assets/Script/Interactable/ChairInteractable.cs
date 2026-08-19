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
    Collider triggerCollider;
    float currentYaw = 0f;
    float currentPitch = 0f;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (sitCam != null) sitCam.Priority = 0;
    }

    private void Update()
    {
        if (!isSitting || sitCamPos == null || sitCam == null) return;

        Vector2 moveInput = movementAction.action.ReadValue<Vector2>();

        // A/D rotates left/right, W/S rotates up/down
        currentYaw += moveInput.x * rotationSpeed * Time.deltaTime;
        currentPitch -= moveInput.y * rotationSpeed * Time.deltaTime;

        currentYaw = Mathf.Clamp(currentYaw, -yawLimit, yawLimit);
        currentPitch = Mathf.Clamp(currentPitch, pitchMin, pitchMax);

        // Always keep sitCam at correct position/rotation
        sitCam.transform.position = sitCamPos.position;
        sitCam.transform.rotation = sitCamPos.rotation * Quaternion.Euler(currentPitch, currentYaw, 0f);

        // ADS: teleport player to sitCamPos so aimCam is at the right spot, then drop sitCam priority
        bool isADS = adsInput != null && adsInput.action.ReadValue<float>() > 0.1f;
        if (isADS)
        {
            sitCam.Priority = 0; // let aimCam take over

            // Align player position/rotation to sitCam so aimCam matches the view
            Player.instance.controller.enabled = false;
            Player.instance.transform.position = sitCamPos.position;
            Player.instance.transform.rotation = sitCam.transform.rotation;
            Player.instance.controller.enabled = true;
        }
        else
        {
            sitCam.Priority = 100; // restore sit cam

            // Return player to sit point when not ADS
            Player.instance.controller.enabled = false;
            Player.instance.transform.position = sitPoint.position;
            Player.instance.transform.rotation = sitPoint.rotation;
            Player.instance.controller.enabled = true;
        }
    }

    public override void Interacted()
    {
        base.Interacted();
        if (isSitting) return;
        StartCoroutine(Co_SitDown());
    }

    IEnumerator Co_SitDown()
    {
        isSitting = true;
        currentYaw = 0f;
        currentPitch = 0f;

        if (triggerCollider != null) triggerCollider.enabled = false;

        Player.instance.pauseMovement = true;

        FadeScreenUI.instance.FadeOut();
        yield return new WaitForSeconds(fadeWaitDuration);

        Player.instance.controller.enabled = false;
        yield return new WaitForEndOfFrame();

        Player.instance.transform.position = sitPoint.position;
        Player.instance.transform.rotation = sitPoint.rotation;
        yield return new WaitForEndOfFrame();

        Player.instance.controller.enabled = true;

        if (sitCam != null) sitCam.Priority = 100;

        FadeScreenUI.instance.FadeIn();
        OnSitDown?.Invoke();

        if (canStandUp && exitSitInput != null)
            exitSitInput.action.performed += OnExitPressed;
    }

    void OnExitPressed(InputAction.CallbackContext ctx)
    {
        if (!isSitting) return;
        StartCoroutine(Co_StandUp());
    }

    IEnumerator Co_StandUp()
    {
        if (exitSitInput != null)
            exitSitInput.action.performed -= OnExitPressed;

        FadeScreenUI.instance.FadeOut();
        yield return new WaitForSeconds(fadeWaitDuration);

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

        Player.instance.pauseMovement = false;
        isSitting = false;

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
